using System.Text.Json.Serialization;

namespace LocalMangaLibrary;

public sealed class LibraryConfig
{
    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = "";

    [JsonPropertyName("structure_csv_path")]
    public string StructureCsvPath { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";
}

public sealed class LibraryIndex
{
    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = "";

    [JsonPropertyName("last_scanned")]
    public string? LastScanned { get; set; }

    [JsonPropertyName("item_count")]
    public int ItemCount { get; set; }

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

public sealed class RecentItem
{
    [JsonPropertyName("folder_path")]
    public string FolderPath { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";
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
