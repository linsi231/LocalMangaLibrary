using System.Diagnostics;
using System.IO;
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
    private readonly ImageCacheService _imageCache;
    private readonly LibraryScanner _scanner;
    private readonly object _scanLock = new();
    private readonly Dictionary<string, ScanJob> _scanJobs = new();
    private readonly object _preloadLock = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _preloadJobs = new(StringComparer.OrdinalIgnoreCase);
    private string _activeScanJobId = "";

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
                "/api/library" when method == "GET" => JsonResponse(env, 200, ApiLibrary()),
                "/api/cache" when method == "GET" => JsonResponse(env, 200, CacheStats()),
                "/api/config" when method == "POST" => JsonResponse(env, 200, await ApiConfigAsync(body)),
                "/api/scan" when method == "POST" => JsonResponse(env, 200, await ApiScanAsync(body)),
                "/api/cache/clear" when method == "POST" => JsonResponse(env, 200, ApiCacheClear()),
                "/api/work-images" when method == "POST" => JsonResponse(env, 200, await ApiWorkImagesAsync(body)),
                "/api/preload-reader" when method == "POST" => JsonResponse(env, 200, await ApiPreloadReaderAsync(body)),
                "/api/preload-reader" when method == "GET" => JsonResponse(env, 200, ApiPreloadReaderStatus(uri)),
                "/api/open-directory" when method == "POST" => JsonResponse(env, 200, await ApiOpenDirectoryAsync(body)),
                "/api/decisions" when method == "POST" => JsonResponse(env, 200, await ApiDecisionsAsync(body)),
                "/api/recent" when method == "POST" => JsonResponse(env, 200, await ApiRecentAsync(body)),
                "/api/work-thumb" when method == "GET" => ApiWorkThumb(env, uri),
                "/api/reader-image" when method == "GET" => ApiReaderImage(env, uri),
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

    public void ClearCacheOnShutdown()
    {
        _storage.ClearCache();
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

        return env.CreateWebResourceResponse(File.OpenRead(path), 200, "OK", $"Content-Type: {mime}\r\nCache-Control: no-cache");
    }

    private object ApiStatus()
    {
        var config = LoadConfig();
        var index = LoadIndex();
        return new
        {
            root_path = config.RootPath,
            structure_csv_path = "",
            root_exists = Directory.Exists(config.RootPath),
            csv_exists = false,
            index_exists = File.Exists(_storage.IndexPath),
            item_count = index.Items.Count,
            last_scanned = index.LastScanned,
            needs_setup = !Directory.Exists(config.RootPath),
            cache = CacheStats(),
        };
    }

    private object ApiLibrary()
    {
        return new
        {
            index = LoadIndex(),
            decisions = _storage.LoadJson(_storage.DecisionsPath, new List<DecisionItem>()),
            recent = _storage.LoadJson(_storage.RecentPath, new List<RecentItem>()).Take(10).ToList(),
            config = LoadConfig(),
        };
    }

    private async Task<object> ApiConfigAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var rootPath = FullPath(GetString(doc, "root_path"));
        var csvPath = "";
        ValidateRoot(rootPath);
        var config = new LibraryConfig { RootPath = rootPath, StructureCsvPath = csvPath, UpdatedAt = FormatService.NowIso() };
        _storage.SaveJson(_storage.ConfigPath, config);
        return new { ok = true, root_path = rootPath, structure_csv_path = csvPath };
    }

    private async Task<object> ApiScanAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var config = LoadConfig();
        var rootPath = FullPath(GetString(doc, "root_path", config.RootPath));
        var csvPath = "";
        ValidateRoot(rootPath);
        _storage.SaveJson(_storage.ConfigPath, new LibraryConfig { RootPath = rootPath, StructureCsvPath = csvPath, UpdatedAt = FormatService.NowIso() });

        var job = new ScanJob
        {
            JobId = Guid.NewGuid().ToString("N"),
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
        _storage.SaveJson(_storage.IndexPath, job.Index);
        _ = Task.Run(() => RunScanJob(job.JobId, rootPath, csvPath));
        return new { ok = true, job_id = job.JobId, job, index = job.Index };
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
            return new { ok = true, job, index = job.Index };
        }
    }

    private object ApiCacheClear()
    {
        _storage.ClearCache();
        return new { ok = true, skipped_count = 0, cache = CacheStats() };
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
        var item = FindLibraryItem(folderPath);
        if (item is null || string.IsNullOrWhiteSpace(item.CoverImage))
        {
            throw new ApiException(404, "当前作品没有封面图片。");
        }
        var thumb = _imageCache.MakeThumbnail(item.CoverImage);
        return string.IsNullOrWhiteSpace(thumb)
            ? FileResponse(env, item.CoverImage, MimeForImage(item.CoverImage))
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
        lock (_preloadLock)
        {
            if (_preloadJobs.TryGetValue(key, out var existing) && Equals(existing["status"], "running"))
            {
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
        }
        _ = Task.Run(() => RunPreload(folderPath));
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
        var decisions = _storage.LoadJson(_storage.DecisionsPath, new List<DecisionItem>());
        decisions.RemoveAll(item => string.Equals(item.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        var decision = new DecisionItem { FolderPath = folderPath, Status = status, Note = note, UpdatedAt = FormatService.NowIso() };
        decisions.Add(decision);
        _storage.SaveJson(_storage.DecisionsPath, decisions);
        return new { ok = true, decision, decisions };
    }

    private async Task<object> ApiRecentAsync(Stream? body)
    {
        var doc = await ReadBodyAsync(body);
        var folderPath = GetString(doc, "folder_path");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ApiException(400, "缺少目录路径。");
        }
        var recent = _storage.LoadJson(_storage.RecentPath, new List<RecentItem>());
        recent.RemoveAll(item => string.Equals(item.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, new RecentItem { FolderPath = folderPath, UpdatedAt = FormatService.NowIso() });
        recent = recent.Take(10).ToList();
        _storage.SaveJson(_storage.RecentPath, recent);
        return new { ok = true, recent };
    }

    private void RunScanJob(string jobId, string rootPath, string csvPath)
    {
        var items = new List<WorkItem>();
        LibraryIndex extra;
        List<(string Folder, Dictionary<string, object?> Metadata)> targets;
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
                    targets = _scanner.DirectTargets(rootPath);
                    extra = new LibraryIndex
                    {
                        ScanSource = "direct_fallback",
                        StructureCsvPath = csvPath,
                        CsvWarning = "结构 CSV 与当前根目录不匹配，已回退到普通一级目录扫描。",
                        CsvAttempt = attempt,
                    };
                }
            }
            else
            {
                targets = _scanner.DirectTargets(rootPath);
                extra = new LibraryIndex();
            }

            UpdateScan(jobId, job =>
            {
                job.Status = "running";
                job.Message = "正在扫描作品...";
                job.Total = targets.Count;
                job.Done = 0;
                job.Items = [];
                job.Index = MakeIndex(rootPath, [], extra);
            });

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var item = _scanner.ApplyMetadata(_scanner.ScanWork(target.Folder, false), target.Metadata);
                items.Add(item);
                UpdateScan(jobId, job =>
                {
                    job.Done = i + 1;
                    job.Items = items.ToList();
                    job.Index = MakeIndex(rootPath, items, extra);
                    job.Message = $"已扫描 {i + 1} / {targets.Count} 个作品";
                });
                if (_activeScanJobId == jobId && (i == 0 || (i + 1) % 5 == 0))
                {
                    _storage.SaveJson(_storage.IndexPath, MakeIndex(rootPath, items, extra));
                }
            }

            var finalIndex = MakeIndex(rootPath, items, extra);
            if (_activeScanJobId == jobId)
            {
                _storage.SaveJson(_storage.IndexPath, finalIndex);
            }
            UpdateScan(jobId, job =>
            {
                job.Status = "done";
                job.Done = targets.Count;
                job.Items = items.ToList();
                job.Index = finalIndex;
                job.Message = $"扫描完成：{items.Count} 个作品";
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
    }

    private void RunPreload(string folderPath)
    {
        var key = Path.GetFullPath(folderPath);
        var images = _scanner.ListImages(folderPath);
        var failed = 0;
        UpdatePreload(key, job => { job["status"] = "running"; job["total"] = images.Count; job["done"] = 0; });
        for (var i = 0; i < images.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(_imageCache.MakeReaderImage(images[i])))
            {
                failed++;
            }
            UpdatePreload(key, job => { job["done"] = i + 1; job["failed"] = failed; });
        }
        UpdatePreload(key, job => { job["status"] = "done"; job["completed_at"] = FormatService.NowIso(); });
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

    private LibraryIndex MakeIndex(string rootPath, List<WorkItem> items, LibraryIndex extra)
    {
        return new LibraryIndex
        {
            RootPath = rootPath,
            LastScanned = FormatService.NowIso(),
            ItemCount = items.Count,
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
        List<string> files = Directory.Exists(_storage.CacheDir)
            ? Directory.EnumerateFiles(_storage.CacheDir, "*", SearchOption.AllDirectories).ToList()
            : new List<string>();
        var size = files.Sum(path =>
        {
            try { return new FileInfo(path).Length; }
            catch { return 0L; }
        });
        return new
        {
            cache_path = _storage.CacheDir,
            thumbs_path = _storage.ThumbDir,
            reader_path = _storage.ReaderDir,
            file_count = files.Count,
            total_size = size,
            total_size_label = FormatService.FormatBytes(size),
        };
    }

    private int ClearDir(string dir)
    {
        Directory.CreateDirectory(dir);
        var skipped = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.Delete(file); }
            catch { skipped++; }
        }
        return skipped;
    }

    private bool KnownLibraryFolder(string folderPath)
    {
        var full = Path.GetFullPath(folderPath);
        if (LoadIndex().Items.Any(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        lock (_scanLock)
        {
            return _scanJobs.Values.Any(job => job.Items.Any(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private WorkItem? FindLibraryItem(string folderPath)
    {
        var full = Path.GetFullPath(folderPath);
        var item = LoadIndex().Items.FirstOrDefault(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            return item;
        }
        lock (_scanLock)
        {
            return _scanJobs.Values.SelectMany(job => job.Items).FirstOrDefault(item => string.Equals(Path.GetFullPath(item.FolderPath), full, StringComparison.OrdinalIgnoreCase));
        }
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

    private LibraryConfig LoadConfig() => _storage.LoadJson(_storage.ConfigPath, new LibraryConfig());
    private LibraryIndex LoadIndex() => _storage.LoadJson(_storage.IndexPath, new LibraryIndex());

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

    private static string FullPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
    }

    private static string MimeForImage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
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
