using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace LocalMangaLibrary;

public sealed class LocalApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly StorageService _storage = new();
    private readonly RuntimeStateService _runtime = new();
    private readonly ImageCacheService _imageCache;
    private readonly LibraryScanner _scanner;
    private readonly object _scanLock = new();
    private readonly Dictionary<string, ScanJob> _scanJobs = new();
    private readonly Dictionary<string, CancellationTokenSource> _scanCancellations = new();
    private readonly object _preloadLock = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _preloadJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _preloadCancellations = new(StringComparer.OrdinalIgnoreCase);
    private string _activeScanJobId = "";

    public event Action? ExitRequested;
    public string WebViewUserDataDir => _storage.WebViewDir;

    public LocalApiService()
    {
        _imageCache = new ImageCacheService(_storage);
        _scanner = new LibraryScanner(_imageCache);
    }

    public async Task<CoreWebView2WebResourceResponse> HandleAsync(CoreWebView2Environment env, string method, Uri uri, Stream? body)
    {
        try
        {
            var path = uri.AbsolutePath;
            if (path.StartsWith("/thumbs/", StringComparison.OrdinalIgnoreCase))
            {
                return FileResponse(env, Path.Combine(_storage.ThumbDir, Uri.UnescapeDataString(Path.GetFileName(path))), "image/jpeg");
            }
            if (path.StartsWith("/reader-cache/", StringComparison.OrdinalIgnoreCase))
            {
                return FileResponse(env, Path.Combine(_storage.ReaderDir, Uri.UnescapeDataString(Path.GetFileName(path))), "image/jpeg");
            }

            return path switch
            {
                "/api/status" when method == "GET" => JsonResponse(env, 200, ApiStatus()),
                "/api/app/info" when method == "GET" => JsonResponse(env, 200, ApiAppInfo()),
                "/api/library" when method == "GET" => JsonResponse(env, 200, ApiLibrary()),
                "/api/library/query" when method == "GET" => JsonResponse(env, 200, ApiLibraryQuery(uri)),
                "/api/library/ui-state" when method == "GET" => JsonResponse(env, 200, ApiLibraryUiState()),
                "/api/library/ui-state" when method == "POST" => JsonResponse(env, 200, await ApiLibraryUiStateAsync(body)),
                "/api/cache" when method == "GET" => JsonResponse(env, 200, CacheStats()),
                "/api/cache/status" when method == "GET" => JsonResponse(env, 200, CacheStats()),
                "/api/config" when method == "POST" => JsonResponse(env, 200, await ApiConfigAsync(body)),
                "/api/scan" when method == "POST" => JsonResponse(env, 200, await ApiScanAsync(body)),
                "/api/cache/clear" when method == "POST" => JsonResponse(env, 200, ApiCacheClear()),
                "/api/cache/cleanup" when method == "POST" => JsonResponse(env, 200, ApiCacheClear()),
                "/api/work-images" when method == "POST" => JsonResponse(env, 200, await ApiWorkImagesAsync(body)),
                "/api/preload-reader" when method == "POST" => JsonResponse(env, 200, await ApiPreloadReaderAsync(body)),
                "/api/preload-reader" when method == "GET" => JsonResponse(env, 200, ApiPreloadReaderStatus(uri)),
                "/api/open-directory" when method == "POST" => JsonResponse(env, 200, await ApiOpenDirectoryAsync(body)),
                "/api/decisions" when method == "GET" => JsonResponse(env, 200, ApiDecisionsList()),
                "/api/decisions" when method == "POST" => JsonResponse(env, 200, await ApiDecisionsAsync(body)),
                "/api/actions/delete-log" when method == "POST" => JsonResponse(env, 200, await ApiDeleteLogAsync(body)),
                "/api/settings/theme" when method == "POST" => JsonResponse(env, 200, await ApiThemeAsync(body)),
                "/api/settings/library-view-mode" when method == "POST" => JsonResponse(env, 200, await ApiLibraryViewModeAsync(body)),
                "/api/recent" when method == "GET" => JsonResponse(env, 200, ApiRecentList()),
                "/api/recent" when method == "POST" => JsonResponse(env, 200, await ApiRecentAsync(body)),
                "/api/history/roots" when method == "GET" => JsonResponse(env, 200, ApiRootHistoryList()),
                "/api/history/roots/open" when method == "POST" => JsonResponse(env, 200, await ApiRootHistoryOpenAsync(body)),
                "/api/history/roots/clear" when method == "POST" => JsonResponse(env, 200, ApiRootHistoryClear()),
                "/api/history/recent" when method == "GET" => JsonResponse(env, 200, ApiRecentList()),
                "/api/history/recent/update" when method == "POST" => JsonResponse(env, 200, await ApiRecentAsync(body)),
                "/api/history/recent/clear" when method == "POST" => JsonResponse(env, 200, ApiRecentClear()),
                "/api/history/clear" when method == "POST" => JsonResponse(env, 200, ApiHistoryClear()),
                "/api/open-url" when method == "POST" => JsonResponse(env, 200, await ApiOpenUrlAsync(body)),
                "/api/app/exit" when method == "POST" => JsonResponse(env, 200, ApiExit()),
                "/api/work-thumb" when method == "GET" => ApiWorkThumb(env, uri),
                "/api/reader-image" when method == "GET" => ApiReaderImage(env, uri),
                _ when path.StartsWith("/api/scan/", StringComparison.OrdinalIgnoreCase)
                    && path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase)
                    && method == "POST" => JsonResponse(env, 200, ApiScanCancel(path)),
                _ when path.StartsWith("/api/scan/", StringComparison.OrdinalIgnoreCase) => JsonResponse(env, 200, ApiScanStatus(path)),
                _ => JsonResponse(env, 404, new { ok = false, error = "接口不存在。" }),
            };
        }
        catch (ApiException ex)
        {
            return JsonResponse(env, ex.Status, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            return JsonResponse(env, 500, new { ok = false, error = ex.Message });
        }
    }

    public CoreWebView2WebResourceResponse JsonResponse(CoreWebView2Environment env, int status, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return Response(env, status, json, "application/json; charset=utf-8");
    }

    public CacheCleanupResult ClearCacheOnShutdown()
    {
        CancelBackgroundWork();
        _runtime.ClearAll();
        return _storage.ClearSessionOnShutdown();
    }

    public void CancelBackgroundWork()
    {
        lock (_scanLock)
        {
            foreach (var cancellation in _scanCancellations.Values)
            {
                cancellation.Cancel();
            }
        }

        lock (_preloadLock)
        {
            foreach (var cancellation in _preloadCancellations.Values)
            {
                cancellation.Cancel();
            }
        }
    }

    private void CancelActiveScan()
    {
        lock (_scanLock)
        {
            foreach (var cancellation in _scanCancellations.Values)
            {
                cancellation.Cancel();
            }

            _activeScanJobId = "";
        }
    }

    private static CoreWebView2WebResourceResponse Response(CoreWebView2Environment env, int status, string text, string contentType)
    {
        return env.CreateWebResourceResponse(
            new MemoryStream(Encoding.UTF8.GetBytes(text)),
            status,
            status == 200 ? "OK" : "Error",
            $"Content-Type: {contentType}\r\nCache-Control: no-cache");
    }

    private static CoreWebView2WebResourceResponse FileResponse(CoreWebView2Environment env, string path, string mime)
    {
        if (!File.Exists(path))
        {
            return Response(env, 404, "Not Found", "text/plain; charset=utf-8");
        }

        return env.CreateWebResourceResponse(new MemoryStream(File.ReadAllBytes(path)), 200, "OK", $"Content-Type: {mime}\r\nCache-Control: no-cache");
    }

    private object ApiStatus()
    {
        var snapshot = _runtime.Snapshot();
        var config = snapshot.Config;
        var index = snapshot.CurrentIndex;
        var indexMatchesRoot = SamePath(index.RootPath, config.RootPath) && index.Items.Count > 0;
        return new
        {
            root_path = config.RootPath,
            theme = string.IsNullOrWhiteSpace(config.Theme) ? "blue" : config.Theme,
            library_view_mode = string.IsNullOrWhiteSpace(config.LibraryViewMode) ? "grid" : config.LibraryViewMode,
            root_version = snapshot.RootVersion.ToString("N"),
            structure_csv_path = "",
            root_exists = Directory.Exists(config.RootPath),
            csv_exists = false,
            index_exists = indexMatchesRoot,
            index_matches_root = indexMatchesRoot,
            item_count = indexMatchesRoot ? index.Items.Count : 0,
            last_scanned = indexMatchesRoot ? index.LastScanned : null,
            needs_setup = !Directory.Exists(config.RootPath),
            cache = CacheStats(),
            app = ApiAppInfo(),
        };
    }

    private static object ApiAppInfo() => new
    {
        ok = true,
        name = AppInfo.Name,
        display_version = AppInfo.DisplayVersion,
        internal_version = AppInfo.InternalVersion,
        assembly_version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
        architecture = AppInfo.Architecture,
        description = AppInfo.Description,
        github_url = AppInfo.GitHubRepositoryUrl,
        releases_url = AppInfo.GitHubReleasesUrl,
        new_features = AppInfo.NewFeatures,
        bug_fixes = AppInfo.BugFixes,
    };

    private object ApiLibrary()
    {
        var snapshot = _runtime.Snapshot();
        return new
        {
            index = snapshot.CurrentIndex,
            decisions = snapshot.Decisions,
            recent = snapshot.Recent.Take(100).ToList(),
            config = snapshot.Config,
            root_version = snapshot.RootVersion.ToString("N"),
        };
    }

    private object ApiLibraryQuery(Uri uri)
    {
        var snapshot = _runtime.Snapshot();
        var index = snapshot.CurrentIndex;
        var config = snapshot.Config;
        if (!SamePath(index.RootPath, config.RootPath) || index.Items.Count == 0)
        {
            var pageSizeFallback = Math.Clamp(QueryInt(uri, "pageSize", 120), 1, 500);
            return new
            {
                ok = true,
                needs_scan = true,
                message = "当前库还没有可用索引，请刷新索引。",
                items = new List<WorkItem>(),
                total_count = 0,
                page = 1,
                page_size = pageSizeFallback,
                total_pages = 1,
                index_stats = new
                {
                    root_path = config.RootPath,
                    indexed_root_path = index.RootPath,
                    last_scanned = (string?)null,
                    item_count = 0,
                    total_image_count = 0,
                    scanned_directory_count = 0,
                    skipped_directory_count = 0,
                    error_count = 0,
                    errors_sample = new List<string>(),
                    index_matches_root = false,
                },
                root_version = snapshot.RootVersion.ToString("N"),
            };
        }
        var decisions = snapshot.Decisions;
        var decisionMap = decisions
            .GroupBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var query = Query(uri, "q").Trim();
        var sort = Query(uri, "sort").Trim();
        var status = Query(uri, "status").Trim();
        var page = Math.Max(1, QueryInt(uri, "page", 1));
        var pageSize = Math.Clamp(QueryInt(uri, "pageSize", 120), 1, 500);

        IEnumerable<WorkItem> items = index.Items;
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item =>
                item.FolderName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.FolderPath.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (item.RelativePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(status, "missing", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(item => item.IsMissing);
            }
            else
            {
                items = items.Where(item =>
                {
                    var hasDecision = decisionMap.TryGetValue(item.FolderPath, out var decision);
                    if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                    {
                        return !hasDecision || string.Equals(decision!.Status, status, StringComparison.OrdinalIgnoreCase);
                    }

                    return hasDecision && string.Equals(decision!.Status, status, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        items = SortItems(items, sort);
        var materialized = items.ToList();
        var totalCount = materialized.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var pageItems = materialized.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new
        {
            ok = true,
            items = pageItems,
            total_count = totalCount,
            page,
            page_size = pageSize,
            total_pages = totalPages,
            index_stats = new
            {
                root_path = index.RootPath,
                last_scanned = index.LastScanned,
                item_count = index.ItemCount,
                total_image_count = index.TotalImageCount,
                scanned_directory_count = index.ScannedDirectoryCount,
                skipped_directory_count = index.SkippedDirectoryCount,
                error_count = index.ErrorCount,
                errors_sample = index.ErrorsSample,
            },
            root_version = snapshot.RootVersion.ToString("N"),
        };
    }

    private object ApiLibraryUiState()
    {
        var snapshot = _runtime.Snapshot();
        return new
        {
            ok = true,
            ui_state = snapshot.LibraryUiState,
            root_version = snapshot.RootVersion.ToString("N"),
        };
    }

    private async Task<object> ApiLibraryUiStateAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var snapshot = _runtime.Snapshot();
        var rootVersion = GetString(doc, "root_version", snapshot.RootVersion.ToString("N"));
        if (!string.Equals(rootVersion, snapshot.RootVersion.ToString("N"), StringComparison.OrdinalIgnoreCase))
        {
            return new { ok = false, ignored = true, reason = "RootVersion 不一致，已丢弃旧库位置状态。" };
        }

        var uiState = new LibraryUiState
        {
            SelectedWorkPath = GetString(doc, "selected_work_path"),
            SelectedWorkIndex = GetInt(doc, "selected_work_index", -1),
            ScrollOffset = GetDouble(doc, "scroll_offset", 0),
            CurrentPage = Math.Max(1, GetInt(doc, "current_page", 1)),
            SearchQuery = GetString(doc, "search_query"),
            SortMode = GetString(doc, "sort_mode", "name"),
            FilterMode = GetString(doc, "filter_mode", "all"),
            ViewMode = GetString(doc, "view_mode", "grid"),
            RootVersion = rootVersion,
        };
        _runtime.SetLibraryUiState(uiState);
        return new { ok = true, ui_state = uiState };
    }

    private async Task<object> ApiConfigAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var rootPath = FullPath(GetString(doc, "root_path"));
        var csvPath = "";
        ValidateRoot(rootPath);
        CancelActiveScan();
        var rootVersion = _runtime.ReplaceRoot(rootPath);
        UpdateRootHistoryOpened(rootPath);
        return new { ok = true, root_path = rootPath, structure_csv_path = csvPath, root_version = rootVersion.ToString("N"), needs_scan = true };
    }

    private async Task<object> ApiScanAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var config = LoadConfig();
        var rootPath = FullPath(GetString(doc, "root_path", config.RootPath));
        var csvPath = "";
        ValidateRoot(rootPath);
        CancelActiveScan();
        var rootVersion = _runtime.BeginRescan(rootPath);

        var job = new ScanJob
        {
            JobId = Guid.NewGuid().ToString("N"),
            RootVersion = rootVersion.ToString("N"),
            Status = "queued",
            Message = "扫描任务已创建",
            RootPath = rootPath,
            StructureCsvPath = csvPath,
            StartedAt = FormatService.NowIso(),
            UpdatedAt = FormatService.NowIso(),
            Index = MakeIndex(rootPath, [], new LibraryIndex { ScanSource = "pending", StructureCsvPath = csvPath }),
        };
        lock (_scanLock)
        {
            _activeScanJobId = job.JobId;
            _scanJobs[job.JobId] = job;
        }
        var cancellation = new CancellationTokenSource();
        lock (_scanLock)
        {
            _scanCancellations[job.JobId] = cancellation;
        }
        _ = Task.Run(() => RunScanJob(job.JobId, rootVersion, rootPath, csvPath, cancellation.Token));
        return new { ok = true, job_id = job.JobId, root_version = rootVersion.ToString("N"), job = ScanSnapshot(job), index = SlimIndex(job.Index) };
    }

    private object ApiScanStatus(string path)
    {
        var jobId = path.Split('/').Last();
        lock (_scanLock)
        {
            if (!_scanJobs.TryGetValue(jobId, out var job))
            {
                throw new ApiException(404, "扫描任务不存在。");
            }
            return new { ok = true, job = ScanSnapshot(job), index = SlimIndex(job.Index) };
        }
    }

    private object ApiScanCancel(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var jobId = parts.Length >= 3 ? parts[^2] : "";
        lock (_scanLock)
        {
            if (!_scanJobs.TryGetValue(jobId, out var job))
            {
                throw new ApiException(404, "扫描任务不存在。");
            }
            if (_scanCancellations.TryGetValue(jobId, out var cancellation))
            {
                job.IsCancelRequested = true;
                job.Message = "正在取消扫描...";
                cancellation.Cancel();
            }
            return new { ok = true, job = ScanSnapshot(job) };
        }
    }

    private static object ScanSnapshot(ScanJob job)
    {
        return new
        {
            job_id = job.JobId,
            root_version = job.RootVersion,
            status = job.Status,
            message = job.Message,
            root_path = job.RootPath,
            total = job.Total,
            done = job.Done,
            current_path = job.CurrentPath,
            scanned_directory_count = job.ScannedDirectoryCount,
            skipped_directory_count = job.SkippedDirectoryCount,
            error_count = job.ErrorCount,
            total_image_count = job.TotalImageCount,
            errors_sample = job.Index.ErrorsSample,
            started_at = job.StartedAt,
            updated_at = job.UpdatedAt,
            completed_at = job.CompletedAt,
            elapsed_seconds = ElapsedSeconds(job),
            is_cancel_requested = job.IsCancelRequested,
            error = job.Error,
        };
    }

    private static object SlimIndex(LibraryIndex index)
    {
        return new
        {
            root_path = index.RootPath,
            last_scanned = index.LastScanned,
            item_count = index.ItemCount,
            total_image_count = index.TotalImageCount,
            scanned_directory_count = index.ScannedDirectoryCount,
            skipped_directory_count = index.SkippedDirectoryCount,
            error_count = index.ErrorCount,
            errors_sample = index.ErrorsSample,
            scan_source = index.ScanSource,
        };
    }

    private static int ElapsedSeconds(ScanJob job)
    {
        if (!DateTimeOffset.TryParse(job.StartedAt, out var started))
        {
            return 0;
        }
        var end = DateTimeOffset.TryParse(job.CompletedAt, out var completed) ? completed : DateTimeOffset.Now;
        return Math.Max(0, (int)Math.Round((end - started).TotalSeconds));
    }

    private object ApiCacheClear()
    {
        CancelBackgroundWork();
        var cleanup = _storage.ClearCache(recreateSession: true);
        return new { ok = cleanup.Ok, cleanup, cache = CacheStats() };
    }

    private async Task<object> ApiWorkImagesAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        ValidateKnownFolder(folderPath);
        var images = _scanner.ListImages(folderPath)
            .Select((path, index) => new ImageInfo
            {
                Index = index,
                Name = Path.GetFileName(path),
                Url = $"/api/reader-image?folder_path={Uri.EscapeDataString(folderPath)}&index={index}",
            })
            .ToList();
        return new { ok = true, folder_path = folderPath, image_count = images.Count, images };
    }

    private CoreWebView2WebResourceResponse ApiWorkThumb(CoreWebView2Environment env, Uri uri)
    {
        var folderPath = Query(uri, "folder_path");
        ValidateKnownFolder(folderPath);
        var snapshot = _runtime.Snapshot();
        var item = FindLibraryItem(folderPath);
        var coverImage = item?.CoverImage ?? snapshot.Recent
            .FirstOrDefault(entry => string.Equals(entry.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
            ?.CoverPath ?? "";
        if (string.IsNullOrWhiteSpace(coverImage) || !File.Exists(coverImage))
        {
            throw new ApiException(404, "当前作品没有封面图片。");
        }
        var thumb = _imageCache.MakeThumbnail(coverImage);
        return string.IsNullOrWhiteSpace(thumb)
            ? FileResponse(env, coverImage, MimeForImage(coverImage))
            : FileResponse(env, Path.Combine(_storage.ThumbDir, thumb), "image/jpeg");
    }

    private CoreWebView2WebResourceResponse ApiReaderImage(CoreWebView2Environment env, Uri uri)
    {
        var folderPath = Query(uri, "folder_path");
        var indexText = Query(uri, "index");
        ValidateKnownFolder(folderPath);
        if (!int.TryParse(indexText, out var index))
        {
            throw new ApiException(400, "图片序号无效。");
        }
        var images = _scanner.ListImages(folderPath);
        if (index < 0 || index >= images.Count)
        {
            throw new ApiException(404, "图片序号超出范围。");
        }
        var reader = _imageCache.MakeReaderImage(images[index]);
        return string.IsNullOrWhiteSpace(reader)
            ? FileResponse(env, images[index], MimeForImage(images[index]))
            : FileResponse(env, Path.Combine(_storage.ReaderDir, reader), "image/jpeg");
    }

    private async Task<object> ApiPreloadReaderAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        ValidateKnownFolder(folderPath);
        var key = Path.GetFullPath(folderPath);
        Dictionary<string, object?> job;
        var cancellation = new CancellationTokenSource();
        lock (_preloadLock)
        {
            if (_preloadJobs.TryGetValue(key, out var existing) && Equals(existing["status"], "running"))
            {
                cancellation.Dispose();
                return new { ok = true, job = existing, reused = true };
            }
            job = new Dictionary<string, object?>
            {
                ["folder_path"] = folderPath,
                ["status"] = "queued",
                ["total"] = 0,
                ["done"] = 0,
                ["failed"] = 0,
                ["started_at"] = FormatService.NowIso(),
                ["updated_at"] = FormatService.NowIso(),
            };
            _preloadJobs[key] = job;
            _preloadCancellations[key] = cancellation;
        }
        _ = Task.Run(() => RunPreload(folderPath, cancellation.Token));
        return new { ok = true, job, reused = false };
    }

    private object ApiPreloadReaderStatus(Uri uri)
    {
        var key = Path.GetFullPath(Query(uri, "folder_path"));
        lock (_preloadLock)
        {
            if (!_preloadJobs.TryGetValue(key, out var job))
            {
                throw new ApiException(404, "没有预读任务。");
            }
            return new { ok = true, job };
        }
    }

    private async Task<object> ApiOpenDirectoryAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        if (!Directory.Exists(folderPath))
        {
            throw new ApiException(400, "目录不存在或不可访问。");
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"") { UseShellExecute = true });
        return new { ok = true };
    }

    private async Task<object> ApiOpenUrlAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var target = GetString(doc, "target", "repo");
        var url = target.Equals("releases", StringComparison.OrdinalIgnoreCase)
            ? AppInfo.GitHubReleasesUrl
            : AppInfo.GitHubRepositoryUrl;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return new { ok = true, url };
        }
        catch
        {
            throw new ApiException(500, "无法打开浏览器，请手动访问 GitHub 项目页面。");
        }
    }

    private async Task<object> ApiDecisionsAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        var status = GetString(doc, "status", "pending");
        var note = GetString(doc, "note");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ApiException(400, "缺少目录路径。");
        }
        if (!new[] { "keep", "skip", "pending", "check_duplicate" }.Contains(status))
        {
            throw new ApiException(400, "状态值无效。");
        }
        var decisions = _runtime.Snapshot().Decisions;
        decisions.RemoveAll(item => string.Equals(item.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        var decision = new DecisionItem { FolderPath = folderPath, Status = status, Note = note, UpdatedAt = FormatService.NowIso() };
        decisions.Add(decision);
        _runtime.SetDecisions(decisions);
        _storage.SaveJson(_storage.DecisionsPath, decisions);
        return new { ok = true, decision, decisions };
    }

    private async Task<object> ApiDeleteLogAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ApiException(400, "缺少目录路径。");
        }

        var item = FindLibraryItem(folderPath);
        var logs = _runtime.Snapshot().DeleteLogs;
        logs.Insert(0, new DeleteLogItem
        {
            FolderPath = folderPath,
            FolderName = GetString(doc, "folder_name", item?.FolderName ?? Path.GetFileName(folderPath)),
            Note = GetString(doc, "note", "仅记录删除意向，不删除原始文件。"),
            CreatedAt = FormatService.NowIso(),
        });
        logs = logs.Take(1000).ToList();
        _runtime.SetDeleteLogs(logs);
        _storage.SaveJson(_storage.DeleteLogPath, logs);

        var decisions = _runtime.Snapshot().Decisions;
        decisions.RemoveAll(entry => string.Equals(entry.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        decisions.Add(new DecisionItem
        {
            FolderPath = folderPath,
            Status = "skip",
            Note = "delete_requested",
            UpdatedAt = FormatService.NowIso(),
        });
        _runtime.SetDecisions(decisions);
        _storage.SaveJson(_storage.DecisionsPath, decisions);
        return new { ok = true, log = logs.First(), logs_count = logs.Count };
    }

    private async Task<object> ApiThemeAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var theme = GetString(doc, "theme", "blue");
        var allowed = new[] { "blue", "dark", "green", "gray", "purple", "orange" };
        if (!allowed.Contains(theme, StringComparer.OrdinalIgnoreCase))
        {
            throw new ApiException(400, "主题无效。");
        }

        _runtime.SetTheme(theme);
        return new { ok = true, theme };
    }

    private async Task<object> ApiLibraryViewModeAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var mode = GetString(doc, "mode", "grid");
        if (!new[] { "grid", "detail" }.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            throw new ApiException(400, "浏览模式无效。");
        }

        _runtime.SetLibraryViewMode(mode);
        return new { ok = true, library_view_mode = mode };
    }

    private object ApiDecisionsList()
    {
        var decisions = _runtime.Snapshot().Decisions;
        return new { ok = true, decisions };
    }

    private async Task<object> ApiRecentAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ApiException(400, "缺少目录路径。");
        }
        var recent = _runtime.Snapshot().Recent;
        recent.RemoveAll(item => string.Equals(item.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        var existingItem = FindLibraryItem(folderPath);
        var existingRecent = recent
            .FirstOrDefault(item => string.Equals(item.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        var now = FormatService.NowIso();
        var lastPage = GetInt(doc, "last_page", existingRecent?.LastPage ?? 0);
        var readerMode = GetString(doc, "reader_mode", existingRecent?.ReaderMode ?? "horizontal");
        recent.Insert(0, new RecentItem
        {
            FolderPath = folderPath,
            Title = GetString(doc, "title", existingItem?.FolderName ?? existingRecent?.Title ?? Path.GetFileName(folderPath)),
            CoverPath = GetString(doc, "cover_path", existingItem?.CoverImage ?? existingRecent?.CoverPath ?? ""),
            LastPage = Math.Max(0, lastPage),
            ReaderMode = string.IsNullOrWhiteSpace(readerMode) ? "horizontal" : readerMode,
            LastOpenedAt = now,
            UpdatedAt = now,
        });
        recent = recent.Take(100).ToList();
        _runtime.SetRecent(recent);
        _storage.SaveJson(_storage.RecentPath, recent);
        return new { ok = true, recent };
    }

    private object ApiRecentList()
    {
        var snapshot = _runtime.Snapshot();
        var index = snapshot.CurrentIndex;
        var byPath = index.Items.ToDictionary(item => item.FolderPath, StringComparer.OrdinalIgnoreCase);
        var recent = snapshot.Recent.Take(100).ToList();
        var items = recent
            .Select(entry => byPath.TryGetValue(entry.FolderPath, out var item) ? item : null)
            .Where(item => item is not null && !item.IsMissing)
            .Cast<WorkItem>()
            .ToList();
        var records = recent.Select(entry =>
        {
            byPath.TryGetValue(entry.FolderPath, out var item);
            var missing = !Directory.Exists(entry.FolderPath) || item?.IsMissing == true;
            return new
            {
                folder_path = entry.FolderPath,
                title = string.IsNullOrWhiteSpace(entry.Title) ? item?.FolderName ?? Path.GetFileName(entry.FolderPath) : entry.Title,
                cover_path = string.IsNullOrWhiteSpace(entry.CoverPath) ? item?.CoverImage ?? "" : entry.CoverPath,
                last_page = entry.LastPage,
                reader_mode = string.IsNullOrWhiteSpace(entry.ReaderMode) ? "horizontal" : entry.ReaderMode,
                last_opened_at = string.IsNullOrWhiteSpace(entry.LastOpenedAt) ? entry.UpdatedAt : entry.LastOpenedAt,
                updated_at = entry.UpdatedAt,
                missing,
                item,
            };
        }).ToList();
        return new { ok = true, recent, records, items };
    }

    private object ApiRecentClear()
    {
        _runtime.ClearRecent();
        _storage.SaveJson(_storage.RecentPath, new List<RecentItem>());
        return new { ok = true, recent = new List<RecentItem>() };
    }

    private object ApiRootHistoryList()
    {
        var history = _runtime.Snapshot().RootHistory
            .Select(item =>
            {
                item.Missing = !Directory.Exists(item.RootPath);
                return item;
            })
            .OrderByDescending(item => item.LastOpenedAt)
            .Take(20)
            .ToList();
        return new { ok = true, roots = history };
    }

    private object ApiRootHistoryClear()
    {
        _runtime.ClearRootHistory();
        _storage.SaveJson(_storage.RootHistoryPath, new List<RootHistoryItem>());
        return new { ok = true, roots = new List<RootHistoryItem>() };
    }

    private object ApiHistoryClear()
    {
        _runtime.ClearHistory();
        _storage.SaveJson(_storage.RecentPath, new List<RecentItem>());
        _storage.SaveJson(_storage.RootHistoryPath, new List<RootHistoryItem>());
        return new { ok = true, recent = new List<RecentItem>(), roots = new List<RootHistoryItem>() };
    }

    private async Task<object> ApiRootHistoryOpenAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var rootPath = FullPath(GetString(doc, "root_path"));
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ApiException(400, "缺少漫画库根目录。");
        }
        var missing = !Directory.Exists(rootPath);
        var index = LoadIndex();
        var hasIndex = SamePath(index.RootPath, rootPath);
        if (!missing)
        {
            if (!SamePath(LoadConfig().RootPath, rootPath))
            {
                CancelActiveScan();
                _runtime.ReplaceRoot(rootPath);
                hasIndex = false;
            }
            UpdateRootHistoryOpened(rootPath);
        }
        return new
        {
            ok = !missing,
            root_path = rootPath,
            missing,
            has_index = hasIndex,
            item_count = hasIndex ? index.ItemCount : 0,
            last_scanned = hasIndex ? index.LastScanned : null,
            message = missing ? "目录不存在或不可访问。" : hasIndex ? "已切换漫画库目录。" : "已切换漫画库目录，当前库还没有索引，请刷新索引。",
        };
    }

    private object ApiExit()
    {
        Task.Run(async () =>
        {
            await Task.Delay(120);
            ExitRequested?.Invoke();
        });
        return new { ok = true, message = "正在退出..." };
    }

    private void RunScanJob(string jobId, Guid rootVersion, string rootPath, string csvPath, CancellationToken cancellationToken)
    {
        var items = new List<WorkItem>();
        LibraryIndex extra;
        List<(string Folder, Dictionary<string, object?> Metadata)> targets;
        var scanStats = new ScanStats();
        try
        {
            UpdateScan(jobId, job => { job.Status = "preparing"; job.Message = "正在准备扫描目标..."; });
            if (!string.IsNullOrWhiteSpace(csvPath))
            {
                var result = _scanner.CsvTargets(rootPath, csvPath);
                targets = result.Targets;
                extra = result.Extra;
                if (targets.Count == 0 && extra.MissingPathCount > 0)
                {
                    var attempt = extra;
                    var direct = _scanner.DirectTargets(rootPath, cancellationToken);
                    targets = direct.Targets;
                    extra = new LibraryIndex
                    {
                        ScanSource = "direct_fallback",
                        StructureCsvPath = csvPath,
                        CsvWarning = "结构 CSV 与当前根目录不匹配，已回退到普通一级目录扫描。",
                        CsvAttempt = attempt,
                        ScannedDirectoryCount = direct.Stats.ScannedDirectoryCount,
                        SkippedDirectoryCount = direct.Stats.SkippedDirectoryCount,
                        ErrorCount = direct.Stats.ErrorCount,
                        ErrorsSample = direct.Stats.ErrorsSample.ToList(),
                    };
                    scanStats.Merge(direct.Stats);
                }
            }
            else
            {
                var direct = _scanner.DirectTargets(rootPath, cancellationToken);
                targets = direct.Targets;
                extra = direct.Extra;
                scanStats.Merge(direct.Stats);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var previousIndex = LoadIndex();
            if (!SamePath(previousIndex.RootPath, rootPath))
            {
                previousIndex = new LibraryIndex { RootPath = rootPath };
            }
            var previousByPath = previousIndex.Items
                .GroupBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            UpdateScan(jobId, job =>
            {
                job.Status = "running";
                job.Message = "正在扫描作品...";
                job.Total = targets.Count;
                job.Done = 0;
                job.ScannedDirectoryCount = scanStats.ScannedDirectoryCount;
                job.SkippedDirectoryCount = scanStats.SkippedDirectoryCount;
                job.ErrorCount = scanStats.ErrorCount;
                job.TotalImageCount = scanStats.TotalImageCount;
                job.Items = [];
                job.Index = MakeIndex(rootPath, [], extra, scanStats);
            });

            for (var i = 0; i < targets.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = targets[i];
                seenPaths.Add(target.Folder);
                UpdateScan(jobId, job => { job.CurrentPath = target.Folder; });
                var fingerprint = _scanner.ComputeFingerprint(target.Folder, scanStats, cancellationToken);
                WorkItem item;
                if (previousByPath.TryGetValue(target.Folder, out var previous)
                    && !previous.IsMissing
                    && _scanner.SameFingerprint(previous, fingerprint))
                {
                    scanStats.TotalImageCount += fingerprint.ImageCount;
                    item = previous;
                    item.IsMissing = false;
                }
                else
                {
                    var scanned = _scanner.ScanWorkDetailed(target.Folder, false, cancellationToken);
                    scanStats.Merge(scanned.Stats);
                    item = _scanner.ApplyMetadata(scanned.Item, target.Metadata);
                }
                items.Add(item);
                var shouldPublishProgress = i == 0 || i + 1 == targets.Count || (i + 1) % 10 == 0;
                if (shouldPublishProgress)
                {
                    UpdateScan(jobId, job =>
                    {
                        job.Done = i + 1;
                        job.TotalImageCount = scanStats.TotalImageCount;
                        job.ScannedDirectoryCount = scanStats.ScannedDirectoryCount;
                        job.SkippedDirectoryCount = scanStats.SkippedDirectoryCount;
                        job.ErrorCount = scanStats.ErrorCount;
                        job.Index = MakeIndex(rootPath, [], extra, scanStats);
                        job.Message = $"已扫描 {i + 1} / {targets.Count} 个作品";
                    });
                }
            }

            var finalIndex = MakeIndex(rootPath, items, extra, scanStats);
            if (_activeScanJobId == jobId && _runtime.IsCurrent(rootVersion, rootPath))
            {
                _runtime.SetIndex(rootVersion, finalIndex);
                _storage.SaveJson(_storage.IndexPath, finalIndex);
                UpdateRootHistoryScanned(rootPath, finalIndex);
            }
            UpdateScan(jobId, job =>
            {
                job.Status = "done";
                job.Done = targets.Count;
                job.TotalImageCount = scanStats.TotalImageCount;
                job.ScannedDirectoryCount = scanStats.ScannedDirectoryCount;
                job.SkippedDirectoryCount = scanStats.SkippedDirectoryCount;
                job.ErrorCount = scanStats.ErrorCount;
                job.Items = [];
                job.Index = finalIndex;
                job.Message = $"扫描完成：{items.Count} 个作品";
                job.CompletedAt = FormatService.NowIso();
            });
        }
        catch (OperationCanceledException)
        {
            UpdateScan(jobId, job =>
            {
                job.Status = "cancelled";
                job.IsCancelRequested = true;
                job.Message = "扫描已取消，旧索引已保留。";
                job.CompletedAt = FormatService.NowIso();
            });
        }
        catch (Exception ex)
        {
            UpdateScan(jobId, job =>
            {
                job.Status = "error";
                job.Error = ex.Message;
                job.Message = "扫描失败";
                job.CompletedAt = FormatService.NowIso();
            });
        }
        finally
        {
            lock (_scanLock)
            {
                if (_scanCancellations.Remove(jobId, out var cancellation))
                {
                    cancellation.Dispose();
                }
            }
        }
    }

    private void RunPreload(string folderPath, CancellationToken cancellationToken)
    {
        var key = Path.GetFullPath(folderPath);
        try
        {
            var images = _scanner.ListImages(folderPath);
            var failed = 0;
            UpdatePreload(key, job => { job["status"] = "running"; job["total"] = images.Count; job["done"] = 0; });
            for (var i = 0; i < images.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(_imageCache.MakeReaderImage(images[i])))
                {
                    failed++;
                }
                UpdatePreload(key, job => { job["done"] = i + 1; job["failed"] = failed; });
            }
            UpdatePreload(key, job => { job["status"] = "done"; job["completed_at"] = FormatService.NowIso(); });
        }
        catch (OperationCanceledException)
        {
            UpdatePreload(key, job => { job["status"] = "cancelled"; job["completed_at"] = FormatService.NowIso(); });
        }
        finally
        {
            lock (_preloadLock)
            {
                if (_preloadCancellations.Remove(key, out var cancellation))
                {
                    cancellation.Dispose();
                }
            }
        }
    }

    private void UpdateScan(string jobId, Action<ScanJob> update)
    {
        lock (_scanLock)
        {
            if (_scanJobs.TryGetValue(jobId, out var job))
            {
                update(job);
                job.UpdatedAt = FormatService.NowIso();
            }
        }
    }

    private void UpdatePreload(string key, Action<Dictionary<string, object?>> update)
    {
        lock (_preloadLock)
        {
            if (_preloadJobs.TryGetValue(key, out var job))
            {
                update(job);
                job["updated_at"] = FormatService.NowIso();
            }
        }
    }

    private LibraryIndex MakeIndex(string rootPath, List<WorkItem> items, LibraryIndex extra, ScanStats? stats = null)
    {
        var currentStats = stats ?? new ScanStats();
        return new LibraryIndex
        {
            RootPath = rootPath,
            LastScanned = FormatService.NowIso(),
            ItemCount = items.Count,
            TotalImageCount = currentStats.TotalImageCount > 0 ? currentStats.TotalImageCount : items.Sum(item => item.ImageCount),
            ScannedDirectoryCount = Math.Max(extra.ScannedDirectoryCount, currentStats.ScannedDirectoryCount),
            SkippedDirectoryCount = Math.Max(extra.SkippedDirectoryCount, currentStats.SkippedDirectoryCount),
            ErrorCount = Math.Max(extra.ErrorCount, currentStats.ErrorCount),
            ErrorsSample = currentStats.ErrorsSample.Count > 0 ? currentStats.ErrorsSample.ToList() : extra.ErrorsSample.ToList(),
            Items = items.ToList(),
            ScanSource = extra.ScanSource,
            StructureCsvPath = extra.StructureCsvPath,
            StructureCsvRows = extra.StructureCsvRows,
            StructureCsvCandidates = extra.StructureCsvCandidates,
            MissingPathCount = extra.MissingPathCount,
            MissingPathsSample = extra.MissingPathsSample,
            CsvWarning = extra.CsvWarning,
            CsvAttempt = extra.CsvAttempt,
        };
    }

    private object CacheStats()
    {
        return _storage.CacheStatus();
    }

    private void UpdateRootHistoryOpened(string rootPath)
    {
        var history = _runtime.Snapshot().RootHistory;
        history.RemoveAll(item => string.Equals(item.RootPath, rootPath, StringComparison.OrdinalIgnoreCase));
        var existingIndex = LoadIndex();
        history.Insert(0, new RootHistoryItem
        {
            RootPath = rootPath,
            LastOpenedAt = FormatService.NowIso(),
            LastScannedAt = string.Equals(existingIndex.RootPath, rootPath, StringComparison.OrdinalIgnoreCase) ? existingIndex.LastScanned ?? "" : "",
            ItemCount = string.Equals(existingIndex.RootPath, rootPath, StringComparison.OrdinalIgnoreCase) ? existingIndex.ItemCount : 0,
            TotalImageCount = string.Equals(existingIndex.RootPath, rootPath, StringComparison.OrdinalIgnoreCase) ? existingIndex.TotalImageCount : 0,
            Missing = !Directory.Exists(rootPath),
        });
        history = history.Take(20).ToList();
        _runtime.SetRootHistory(history);
        _storage.SaveJson(_storage.RootHistoryPath, history);
    }

    private void UpdateRootHistoryScanned(string rootPath, LibraryIndex index)
    {
        var history = _runtime.Snapshot().RootHistory;
        var now = FormatService.NowIso();
        var existing = history.FirstOrDefault(item => string.Equals(item.RootPath, rootPath, StringComparison.OrdinalIgnoreCase));
        history.RemoveAll(item => string.Equals(item.RootPath, rootPath, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, new RootHistoryItem
        {
            RootPath = rootPath,
            LastOpenedAt = existing?.LastOpenedAt ?? now,
            LastScannedAt = index.LastScanned ?? now,
            ItemCount = index.ItemCount,
            TotalImageCount = index.TotalImageCount,
            Missing = !Directory.Exists(rootPath),
        });
        history = history.Take(20).ToList();
        _runtime.SetRootHistory(history);
        _storage.SaveJson(_storage.RootHistoryPath, history);
    }

    private bool KnownLibraryFolder(string folderPath)
    {
        var full = Path.GetFullPath(folderPath);
        return LoadIndex().Items.Any(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase))
            || _runtime.Snapshot().Recent
                .Any(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase));
    }

    private WorkItem? FindLibraryItem(string folderPath)
    {
        var full = Path.GetFullPath(folderPath);
        return LoadIndex().Items.FirstOrDefault(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateKnownFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new ApiException(400, "目录不存在或不可访问。");
        }
        if (!KnownLibraryFolder(folderPath))
        {
            throw new ApiException(400, "目录不在当前媒体库索引中。");
        }
    }

    private static void ValidateRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ApiException(400, "请填写漫画库根目录。");
        }
        if (!Directory.Exists(rootPath))
        {
            throw new ApiException(400, "目录不存在或不可访问。");
        }
    }

    private static void ValidateCsv(string csvPath)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            return;
        }
        if (!File.Exists(csvPath))
        {
            throw new ApiException(400, "结构 CSV 不存在或不可访问。");
        }
        if (!string.Equals(Path.GetExtension(csvPath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(400, "结构文件必须是 CSV。");
        }
    }

    private LibraryConfig LoadConfig() => _runtime.Snapshot().Config;
    private LibraryIndex LoadIndex() => _runtime.Snapshot().CurrentIndex;

    private static async Task<JsonDocument> ReadBodyAsync(Stream? body)
    {
        if (body is null)
        {
            return JsonDocument.Parse("{}");
        }
        using var reader = new StreamReader(body, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
    }

    private static string GetString(JsonDocument doc, string key, string fallback = "")
    {
        return doc.RootElement.TryGetProperty(key, out var value) ? value.GetString() ?? fallback : fallback;
    }

    private static int GetInt(JsonDocument doc, string key, int fallback = 0)
    {
        if (!doc.RootElement.TryGetProperty(key, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : fallback;
    }

    private static double GetDouble(JsonDocument doc, string key, double fallback = 0)
    {
        if (!doc.RootElement.TryGetProperty(key, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number) ? number : fallback;
    }

    private static string Query(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && string.Equals(Uri.UnescapeDataString(split[0]), key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(split[1].Replace("+", " "));
            }
        }
        return "";
    }

    private static int QueryInt(Uri uri, string key, int fallback)
    {
        return int.TryParse(Query(uri, key), out var value) ? value : fallback;
    }

    private static IEnumerable<WorkItem> SortItems(IEnumerable<WorkItem> items, string sort)
    {
        return sort switch
        {
            "modified" => items.OrderByDescending(item => DateTimeOffset.TryParse(item.LastModified, out var value) ? value : DateTimeOffset.MinValue),
            "files" => items.OrderByDescending(item => item.FileCount).ThenBy(item => item.FolderName, StringComparer.OrdinalIgnoreCase),
            "size" => items.OrderByDescending(item => item.TotalSize).ThenBy(item => item.FolderName, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderBy(item => item.FolderName, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string FullPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
    }

    private static bool SamePath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string MimeForImage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }
}

public sealed class ApiException : Exception
{
    public int Status { get; }

    public ApiException(int status, string message) : base(message)
    {
        Status = status;
    }
}
