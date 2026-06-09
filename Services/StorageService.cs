using System.IO;
using System.Text.Json;

namespace LocalMangaLibrary;

public sealed class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string CacheRootDir { get; }
    public string CacheDir { get; }
    public string SessionName { get; }
    public string ThumbDir { get; }
    public string ReaderDir { get; }
    public string RuntimeDir { get; }
    public string WebViewDir { get; }
    public string LogsDir { get; }
    public string ConfigPath => Path.Combine(RuntimeDir, "settings_session.json");
    public string IndexPath => Path.Combine(RuntimeDir, "current_index.json");
    public string DecisionsPath => Path.Combine(RuntimeDir, "decisions_session.json");
    public string DeleteLogPath => Path.Combine(RuntimeDir, "delete_log_session.json");
    public string RecentPath => Path.Combine(RuntimeDir, "recent_session.json");
    public string RootHistoryPath => Path.Combine(RuntimeDir, "root_history_session.json");
    public string CleanupLogPath => Path.Combine(LogsDir, "session_cleanup.log");

    public StorageService()
    {
        CacheRootDir = Path.Combine(AppContext.BaseDirectory, ".cache");
        SessionName = $"session_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}";
        CleanupOldSessions();
        CacheDir = Path.Combine(CacheRootDir, SessionName);
        ThumbDir = Path.Combine(CacheDir, "thumbs");
        ReaderDir = Path.Combine(CacheDir, "reader");
        RuntimeDir = Path.Combine(CacheDir, "runtime");
        WebViewDir = Path.Combine(CacheDir, "webview2");
        LogsDir = Path.Combine(RuntimeDir, "logs");
        Ensure();
    }

    public void Ensure()
    {
        Directory.CreateDirectory(ThumbDir);
        Directory.CreateDirectory(ReaderDir);
        Directory.CreateDirectory(RuntimeDir);
        Directory.CreateDirectory(WebViewDir);
        Directory.CreateDirectory(LogsDir);
    }

    public T LoadJson<T>(string path, T fallback)
    {
        try
        {
            if (!IsInsideCurrentSession(path) || !File.Exists(path))
            {
                return fallback;
            }

            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public void SaveJson<T>(string path, T value)
    {
        if (!IsInsideCurrentSession(path))
        {
            throw new InvalidOperationException("拒绝写入非当前 session 路径。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions));
        if (File.Exists(path))
        {
            File.Replace(temp, path, null, true);
            return;
        }

        File.Move(temp, path);
    }

    public CacheCleanupResult ClearCache(bool recreateSession = true)
    {
        var attempts = 0;
        var lastError = "";
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            attempts = attempt;
            try
            {
                DeleteDirectoryContents(ThumbDir);
                DeleteDirectoryContents(ReaderDir);
                if (recreateSession)
                {
                    Directory.CreateDirectory(ThumbDir);
                    Directory.CreateDirectory(ReaderDir);
                }
                return BuildCleanupResult(CacheDir, ok: true, attempts, "");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                LogCleanup($"清理本次缓存第 {attempt} 次失败：{ex}");
                Thread.Sleep(350);
            }
        }

        return BuildCleanupResult(CacheDir, ok: false, attempts, lastError);
    }

    public CacheCleanupResult ClearSessionOnShutdown()
    {
        var result = CleanupSession(CacheDir, removeEmptyRoot: true);
        return result;
    }

    public object CacheStatus()
    {
        var sessionStats = DirectoryStats(CacheDir);
        var cacheStats = DirectoryStats(ThumbDir, ReaderDir);
        return new
        {
            ok = true,
            cache_root = CacheRootDir,
            current_session = SessionName,
            session_path = CacheDir,
            runtime_path = RuntimeDir,
            exists = Directory.Exists(CacheDir),
            size_bytes = sessionStats.SizeBytes,
            file_count = sessionStats.FileCount,
            current_session_size_bytes = sessionStats.SizeBytes,
            current_session_file_count = sessionStats.FileCount,
            cache_size_bytes = cacheStats.SizeBytes,
            cache_file_count = cacheStats.FileCount,
            max_size_bytes = (long?)null,
            thumbs_path = ThumbDir,
            reader_path = ReaderDir,
            total_size_label = FormatService.FormatBytes(sessionStats.SizeBytes),
        };
    }

    private void CleanupOldSessions()
    {
        try
        {
            Directory.CreateDirectory(CacheRootDir);
            foreach (var session in Directory.EnumerateDirectories(CacheRootDir, "session_*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(session), SessionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var result = CleanupSession(session, removeEmptyRoot: false);
                if (!result.Ok)
                {
                    LogCleanup($"启动清理旧 session 失败：{session}，{result.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            LogCleanup($"启动清理旧 session 异常：{ex.Message}");
        }
    }

    private CacheCleanupResult CleanupSession(string sessionPath, bool removeEmptyRoot)
    {
        if (!IsCacheSessionPath(sessionPath))
        {
            var unsafeResult = BuildCleanupResult(sessionPath, ok: false, attempts: 0, error: "拒绝清理非 session 缓存目录。");
            LogCleanup($"{unsafeResult.Error} path={sessionPath}");
            return unsafeResult;
        }

        var lastError = "";
        var attempts = 0;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            attempts = attempt;
            try
            {
                if (Directory.Exists(sessionPath))
                {
                    TryDeleteRemainingChildren(sessionPath);
                    Directory.Delete(sessionPath, true);
                }

                if (removeEmptyRoot)
                {
                    TryDeleteCacheRootIfEmpty();
                }

                return BuildCleanupResult(sessionPath, !Directory.Exists(sessionPath), attempts, "");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                LogCleanup($"清理 session 第 {attempt} 次失败：{sessionPath}，{ex}");
                Thread.Sleep(350);
            }
        }

        var result = BuildCleanupResult(sessionPath, ok: false, attempts, lastError);
        LogCleanup($"清理 session 未完全完成：{sessionPath}，{lastError}");
        return result;
    }

    private bool IsInsideCurrentSession(string path)
    {
        try
        {
            var root = Path.GetFullPath(CacheDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private bool IsCacheSessionPath(string path)
    {
        try
        {
            var root = Path.GetFullPath(CacheRootDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(full).StartsWith("session_", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private CacheCleanupResult BuildCleanupResult(string sessionPath, bool ok, int attempts, string error)
    {
        var stats = DirectoryStats(sessionPath);
        return new CacheCleanupResult
        {
            Ok = ok,
            CacheRoot = CacheRootDir,
            CurrentSession = SessionName,
            SessionPath = sessionPath,
            Exists = Directory.Exists(sessionPath),
            IsEmpty = stats.FileCount == 0,
            SizeBytes = stats.SizeBytes,
            FileCount = stats.FileCount,
            Error = error,
            Attempts = attempts,
        };
    }

    private static (int FileCount, long SizeBytes) DirectoryStats(params string[] paths)
    {
        var fileCount = 0;
        long sizeBytes = 0;
        foreach (var path in paths)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    fileCount++;
                    try { sizeBytes += new FileInfo(file).Length; }
                    catch { }
                }
            }
            catch { }
        }
        return (fileCount, sizeBytes);
    }

    private void TryDeleteCacheRootIfEmpty()
    {
        try
        {
            if (Directory.Exists(CacheRootDir) && !Directory.EnumerateFileSystemEntries(CacheRootDir).Any())
            {
                Directory.Delete(CacheRootDir);
            }
        }
        catch (Exception ex)
        {
            LogCleanup($"删除空 .cache 根目录失败：{ex.Message}");
        }
    }

    private void LogCleanup(string message)
    {
        try
        {
            var logDir = Directory.Exists(LogsDir) ? LogsDir : CacheRootDir;
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "session_cleanup.log"), $"[{FormatService.NowIso()}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static void DeleteDirectoryContents(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch { }
        }

        foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(item => item.Length))
        {
            try { Directory.Delete(dir, true); }
            catch { }
        }
    }

    private static void TryDeleteRemainingChildren(string path)
    {
        try
        {
            DeleteDirectoryContents(path);
        }
        catch { }
    }
}
