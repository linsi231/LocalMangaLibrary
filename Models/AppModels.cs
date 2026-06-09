using System.Text.Json.Serialization;

namespace LocalMangaLibrary;

public sealed class LibraryConfig
{
    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = "";

    [JsonPropertyName("structure_csv_path")]
    public string StructureCsvPath { get; set; } = "";

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "blue";

    [JsonPropertyName("library_view_mode")]
    public string LibraryViewMode { get; set; } = "grid";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";
}

public static class AppInfo
{
    public const string Name = "Local Manga Library";
    public const string DisplayVersion = "Alpha 3";
    public const string InternalVersion = "0.3.0-alpha";
    public const string Architecture = "WPF + WebView2";

    public static readonly string[] NewFeatures =
    [
        "重设为单文件一次性本地漫画快速浏览工具",
        "新增本次运行内 RuntimeLibraryState，当前库可原子替换",
        "索引、历史、设置和状态改为 session 生命周期",
        "启动时清理旧 session 缓存，退出时销毁本次 session",
        "切换目录后立即清空旧列表、旧统计和旧筛选",
    ];

    public static readonly string[] BugFixes =
    [
        "修复跨运行旧索引污染当前库的问题",
        "修复多个漫画库合计数据累计的问题",
        "修复旧扫描任务可能覆盖新库 UI 的问题",
    ];
}

public sealed class LibraryIndex
{
    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = "";

    [JsonPropertyName("last_scanned")]
    public string? LastScanned { get; set; }

    [JsonPropertyName("item_count")]
    public int ItemCount { get; set; }

    [JsonPropertyName("total_image_count")]
    public int TotalImageCount { get; set; }

    [JsonPropertyName("scanned_directory_count")]
    public int ScannedDirectoryCount { get; set; }

    [JsonPropertyName("skipped_directory_count")]
    public int SkippedDirectoryCount { get; set; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; set; }

    [JsonPropertyName("errors_sample")]
    public List<string> ErrorsSample { get; set; } = [];

    [JsonPropertyName("items")]
    public List<WorkItem> Items { get; set; } = [];

    [JsonPropertyName("scan_source")]
    public string? ScanSource { get; set; }

    [JsonPropertyName("structure_csv_path")]
    public string? StructureCsvPath { get; set; }

    [JsonPropertyName("structure_csv_rows")]
    public int? StructureCsvRows { get; set; }

    [JsonPropertyName("structure_csv_candidates")]
    public int? StructureCsvCandidates { get; set; }

    [JsonPropertyName("missing_path_count")]
    public int? MissingPathCount { get; set; }

    [JsonPropertyName("missing_paths_sample")]
    public List<string>? MissingPathsSample { get; set; }

    [JsonPropertyName("csv_warning")]
    public string? CsvWarning { get; set; }

    [JsonPropertyName("csv_attempt")]
    public object? CsvAttempt { get; set; }
}

public sealed class WorkItem
{
    [JsonPropertyName("folder_name")]
    public string FolderName { get; set; } = "";

    [JsonPropertyName("folder_path")]
    public string FolderPath { get; set; } = "";

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }

    [JsonPropertyName("image_count")]
    public int ImageCount { get; set; }

    [JsonPropertyName("total_size")]
    public long TotalSize { get; set; }

    [JsonPropertyName("total_size_label")]
    public string TotalSizeLabel { get; set; } = "";

    [JsonPropertyName("first_image")]
    public string FirstImage { get; set; } = "";

    [JsonPropertyName("cover_image")]
    public string CoverImage { get; set; } = "";

    [JsonPropertyName("thumb_url")]
    public string ThumbUrl { get; set; } = "";

    [JsonPropertyName("last_modified")]
    public string LastModified { get; set; } = "";

    [JsonPropertyName("last_write_time_utc")]
    public string LastWriteTimeUtc { get; set; } = "";

    [JsonPropertyName("is_missing")]
    public bool IsMissing { get; set; }

    [JsonPropertyName("relative_path")]
    public string? RelativePath { get; set; }

    [JsonPropertyName("csv_direct_file_count")]
    public int? CsvDirectFileCount { get; set; }

    [JsonPropertyName("csv_total_file_count")]
    public int? CsvTotalFileCount { get; set; }

    [JsonPropertyName("csv_child_folder_count")]
    public int? CsvChildFolderCount { get; set; }
}

public sealed class DecisionItem
{
    [JsonPropertyName("folder_path")]
    public string FolderPath { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";
}

public sealed class DeleteLogItem
{
    [JsonPropertyName("folder_path")]
    public string FolderPath { get; set; } = "";

    [JsonPropertyName("folder_name")]
    public string FolderName { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "mark_delete_requested";

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}

public sealed class RecentItem
{
    [JsonPropertyName("folder_path")]
    public string FolderPath { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("cover_path")]
    public string CoverPath { get; set; } = "";

    [JsonPropertyName("last_page")]
    public int LastPage { get; set; }

    [JsonPropertyName("reader_mode")]
    public string ReaderMode { get; set; } = "horizontal";

    [JsonPropertyName("last_opened_at")]
    public string LastOpenedAt { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";
}

public sealed class RootHistoryItem
{
    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = "";

    [JsonPropertyName("last_opened_at")]
    public string LastOpenedAt { get; set; } = "";

    [JsonPropertyName("last_scanned_at")]
    public string LastScannedAt { get; set; } = "";

    [JsonPropertyName("item_count")]
    public int ItemCount { get; set; }

    [JsonPropertyName("total_image_count")]
    public int TotalImageCount { get; set; }

    [JsonPropertyName("missing")]
    public bool Missing { get; set; }
}

public sealed class CacheCleanupResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("cache_root")]
    public string CacheRoot { get; set; } = "";

    [JsonPropertyName("current_session")]
    public string CurrentSession { get; set; } = "";

    [JsonPropertyName("session_path")]
    public string SessionPath { get; set; } = "";

    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    [JsonPropertyName("is_empty")]
    public bool IsEmpty { get; set; }

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }
}

public sealed class ImageInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

public sealed class ScanJob
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = "";

    [JsonPropertyName("root_version")]
    public string RootVersion { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "queued";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = "";

    [JsonPropertyName("structure_csv_path")]
    public string StructureCsvPath { get; set; } = "";

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("done")]
    public int Done { get; set; }

    [JsonPropertyName("scanned_directory_count")]
    public int ScannedDirectoryCount { get; set; }

    [JsonPropertyName("skipped_directory_count")]
    public int SkippedDirectoryCount { get; set; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; set; }

    [JsonPropertyName("total_image_count")]
    public int TotalImageCount { get; set; }

    [JsonPropertyName("current_path")]
    public string CurrentPath { get; set; } = "";

    [JsonPropertyName("is_cancel_requested")]
    public bool IsCancelRequested { get; set; }

    [JsonPropertyName("items")]
    public List<WorkItem> Items { get; set; } = [];

    [JsonPropertyName("index")]
    public LibraryIndex Index { get; set; } = new();

    [JsonPropertyName("started_at")]
    public string StartedAt { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class CsvFolderRow
{
    public int Index { get; set; }
    public string FolderName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string[] Parts { get; set; } = [];
    public int Depth => Math.Max(0, Parts.Length - 1);
    public int DirectFileCount { get; set; }
    public int TotalFileCount { get; set; }
    public int ChildFolderCount { get; set; }
}

public sealed class ScanStats
{
    [JsonPropertyName("scanned_directory_count")]
    public int ScannedDirectoryCount { get; set; }

    [JsonPropertyName("skipped_directory_count")]
    public int SkippedDirectoryCount { get; set; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; set; }

    [JsonPropertyName("total_image_count")]
    public int TotalImageCount { get; set; }

    [JsonPropertyName("errors_sample")]
    public List<string> ErrorsSample { get; set; } = [];

    public void AddError(string path, Exception ex)
    {
        ErrorCount++;
        if (ErrorsSample.Count < 50)
        {
            ErrorsSample.Add($"{path}: {ex.Message}");
        }
    }

    public void Merge(ScanStats other)
    {
        ScannedDirectoryCount += other.ScannedDirectoryCount;
        SkippedDirectoryCount += other.SkippedDirectoryCount;
        ErrorCount += other.ErrorCount;
        TotalImageCount += other.TotalImageCount;
        foreach (var error in other.ErrorsSample)
        {
            if (ErrorsSample.Count >= 50)
            {
                break;
            }
            ErrorsSample.Add(error);
        }
    }
}

public sealed class ScanDiscoveryResult
{
    public List<(string Folder, Dictionary<string, object?> Metadata)> Targets { get; set; } = [];
    public LibraryIndex Extra { get; set; } = new();
    public ScanStats Stats { get; set; } = new();
}

public sealed class WorkScanResult
{
    public WorkItem Item { get; set; } = new();
    public ScanStats Stats { get; set; } = new();
}

public sealed class WorkFingerprint
{
    public int FileCount { get; set; }
    public int ImageCount { get; set; }
    public long TotalSize { get; set; }
    public DateTime LastWriteTimeUtc { get; set; } = DateTime.MinValue;

    public string LastWriteTimeUtcIso => LastWriteTimeUtc == DateTime.MinValue
        ? ""
        : new DateTimeOffset(DateTime.SpecifyKind(LastWriteTimeUtc, DateTimeKind.Utc)).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
