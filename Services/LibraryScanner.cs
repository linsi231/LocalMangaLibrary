using System.IO;

namespace LocalMangaLibrary;

public sealed class LibraryScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".avif",
    };

    private static readonly string[] CoverHints = ["cover", "front", "封面", "表紙", "hyoushi"];

    private readonly ImageCacheService _imageCache;
    private readonly CsvStructureService _csv = new();

    public LibraryScanner(ImageCacheService imageCache)
    {
        _imageCache = imageCache;
    }

    public List<string> ListImages(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var stats = new ScanStats();
        return SafeEnumerateFiles(folderPath, true, stats)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, NaturalPathComparer.Instance)
            .ToList();
    }

    public WorkItem ScanWork(string folderPath, bool generateThumbnail) => ScanWorkDetailed(folderPath, generateThumbnail, CancellationToken.None).Item;

    public WorkScanResult ScanWorkDetailed(string folderPath, bool generateThumbnail, CancellationToken cancellationToken = default)
    {
        var dir = new DirectoryInfo(folderPath);
        var fileCount = 0;
        var imageCount = 0;
        long totalSize = 0;
        var lastModified = dir.Exists ? dir.LastWriteTime : DateTime.MinValue;
        var imagePaths = new List<string>();
        var stats = new ScanStats();

        foreach (var filePath in SafeEnumerateFiles(folderPath, true, stats, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var file = new FileInfo(filePath);
                fileCount++;
                totalSize += file.Length;
                if (file.LastWriteTime > lastModified)
                {
                    lastModified = file.LastWriteTime;
                }

                if (ImageExtensions.Contains(file.Extension))
                {
                    imageCount++;
                    imagePaths.Add(file.FullName);
                }
            }
            catch
            {
                stats.AddError(filePath, new IOException("无法读取文件信息。"));
                // Ignore inaccessible files; browsing should continue.
            }
        }

        imagePaths = imagePaths.OrderBy(path => path, NaturalPathComparer.Instance).ToList();
        var firstImage = imagePaths.FirstOrDefault() ?? "";
        var coverImage = PickCover(imagePaths);
        var thumbName = generateThumbnail && !string.IsNullOrEmpty(coverImage) ? _imageCache.MakeThumbnail(coverImage) : "";

        stats.TotalImageCount = imageCount;

        return new WorkScanResult
        {
            Item = new WorkItem
            {
                FolderName = dir.Name,
                FolderPath = dir.FullName,
                FileCount = fileCount,
                ImageCount = imageCount,
                TotalSize = totalSize,
                TotalSizeLabel = FormatService.FormatBytes(totalSize),
                FirstImage = firstImage,
                CoverImage = coverImage,
                ThumbUrl = string.IsNullOrEmpty(thumbName) ? "" : $"/thumbs/{thumbName}",
                LastModified = new DateTimeOffset(lastModified).ToString("yyyy-MM-ddTHH:mm:sszzz"),
                LastWriteTimeUtc = new DateTimeOffset(lastModified.ToUniversalTime()).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                IsMissing = false,
            },
            Stats = stats,
        };
    }

    public WorkFingerprint ComputeFingerprint(string folderPath, ScanStats stats, CancellationToken cancellationToken = default)
    {
        var fingerprint = new WorkFingerprint();
        foreach (var filePath in SafeEnumerateFiles(folderPath, true, stats, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var file = new FileInfo(filePath);
                fingerprint.FileCount++;
                fingerprint.TotalSize += file.Length;
                if (file.LastWriteTimeUtc > fingerprint.LastWriteTimeUtc)
                {
                    fingerprint.LastWriteTimeUtc = file.LastWriteTimeUtc;
                }
                if (ImageExtensions.Contains(file.Extension))
                {
                    fingerprint.ImageCount++;
                }
            }
            catch (Exception ex) when (IsFilesystemException(ex))
            {
                stats.AddError(filePath, ex);
            }
        }
        return fingerprint;
    }

    public bool SameFingerprint(WorkItem item, WorkFingerprint fingerprint)
    {
        return item.FileCount == fingerprint.FileCount
            && item.ImageCount == fingerprint.ImageCount
            && item.TotalSize == fingerprint.TotalSize
            && string.Equals(item.LastWriteTimeUtc, fingerprint.LastWriteTimeUtcIso, StringComparison.OrdinalIgnoreCase);
    }

    public ScanDiscoveryResult DirectTargets(string rootPath, CancellationToken cancellationToken = default)
    {
        var result = new ScanDiscoveryResult
        {
            Extra = new LibraryIndex { ScanSource = "recursive_direct" },
        };

        foreach (var child in SafeEnumerateDirectories(rootPath, result.Stats, cancellationToken).OrderBy(path => path, NaturalPathComparer.Instance))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverWorkTargets(child, result, cancellationToken);
        }

        result.Targets = result.Targets
            .OrderBy(target => target.Folder, NaturalPathComparer.Instance)
            .ToList();
        result.Extra.ScannedDirectoryCount = result.Stats.ScannedDirectoryCount;
        result.Extra.SkippedDirectoryCount = result.Stats.SkippedDirectoryCount;
        result.Extra.ErrorCount = result.Stats.ErrorCount;
        result.Extra.ErrorsSample = result.Stats.ErrorsSample.ToList();
        return result;
    }

    public (List<(string Folder, Dictionary<string, object?> Metadata)> Targets, LibraryIndex Extra) CsvTargets(string rootPath, string csvPath)
    {
        var rows = _csv.Read(csvPath);
        var workRows = _csv.SelectWorkRows(rows);
        var targets = new List<(string Folder, Dictionary<string, object?> Metadata)>();
        var missing = new List<string>();
        foreach (var row in workRows)
        {
            var folder = Path.Combine(new[] { rootPath }.Concat(row.Parts).ToArray());
            if (!Directory.Exists(folder))
            {
                missing.Add(row.RelativePath);
                continue;
            }

            targets.Add((folder, new Dictionary<string, object?>
            {
                ["relative_path"] = row.RelativePath,
                ["csv_direct_file_count"] = row.DirectFileCount,
                ["csv_total_file_count"] = row.TotalFileCount,
                ["csv_child_folder_count"] = row.ChildFolderCount,
            }));
        }

        return (targets, new LibraryIndex
        {
            ScanSource = "structure_csv",
            StructureCsvPath = Path.GetFullPath(csvPath),
            StructureCsvRows = rows.Count,
            StructureCsvCandidates = workRows.Count,
            MissingPathCount = missing.Count,
            MissingPathsSample = missing.Take(50).ToList(),
        });
    }

    public WorkItem ApplyMetadata(WorkItem item, Dictionary<string, object?> metadata)
    {
        if (metadata.TryGetValue("relative_path", out var relativePath))
        {
            item.RelativePath = relativePath?.ToString();
        }
        if (metadata.TryGetValue("csv_direct_file_count", out var direct))
        {
            item.CsvDirectFileCount = Convert.ToInt32(direct);
        }
        if (metadata.TryGetValue("csv_total_file_count", out var total))
        {
            item.CsvTotalFileCount = Convert.ToInt32(total);
        }
        if (metadata.TryGetValue("csv_child_folder_count", out var children))
        {
            item.CsvChildFolderCount = Convert.ToInt32(children);
        }
        return item;
    }

    private static string PickCover(List<string> images)
    {
        foreach (var image in images)
        {
            var stem = Path.GetFileNameWithoutExtension(image).ToLowerInvariant();
            if (CoverHints.Any(hint => stem.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            {
                return image;
            }
        }

        return images.FirstOrDefault() ?? "";
    }

    private static void DiscoverWorkTargets(string folder, ScanDiscoveryResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        result.Stats.ScannedDirectoryCount++;
        var directImages = SafeEnumerateFiles(folder, false, result.Stats, cancellationToken)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, NaturalPathComparer.Instance)
            .ToList();
        if (directImages.Count > 0)
        {
            result.Targets.Add((folder, new Dictionary<string, object?>
            {
                ["direct_image_count"] = directImages.Count,
            }));
            return;
        }

        var children = SafeEnumerateDirectories(folder, result.Stats, cancellationToken)
            .OrderBy(path => path, NaturalPathComparer.Instance)
            .ToList();
        if (children.Count == 0)
        {
            result.Stats.SkippedDirectoryCount++;
            return;
        }

        foreach (var child in children)
        {
            DiscoverWorkTargets(child, result, cancellationToken);
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folder, bool recursive, ScanStats stats, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder).ToList();
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            stats.AddError(folder, ex);
            yield break;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }

        if (!recursive)
        {
            yield break;
        }

        foreach (var child in SafeEnumerateDirectories(folder, stats, cancellationToken))
        {
            foreach (var file in SafeEnumerateFiles(child, true, stats, cancellationToken))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string folder, ScanStats stats, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(folder).ToList();
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            stats.AddError(folder, ex);
            yield break;
        }

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return directory;
        }
    }

    private static bool IsFilesystemException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or PathTooLongException or DirectoryNotFoundException or NotSupportedException;
}

public sealed class NaturalPathComparer : IComparer<string>
{
    public static readonly NaturalPathComparer Instance = new();

    private NaturalPathComparer()
    {
    }

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }
        if (left is null)
        {
            return -1;
        }
        if (right is null)
        {
            return 1;
        }

        return CompareText(Normalize(left), Normalize(right));
    }

    private static string Normalize(string value) => value.Replace('\\', '/');

    private static int CompareText(string left, string right)
    {
        var i = 0;
        var j = 0;
        while (i < left.Length && j < right.Length)
        {
            var leftChar = left[i];
            var rightChar = right[j];
            if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
            {
                var numberCompare = CompareNumber(left, ref i, right, ref j);
                if (numberCompare != 0)
                {
                    return numberCompare;
                }
                continue;
            }

            var charCompare = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
            if (charCompare != 0)
            {
                return charCompare;
            }

            i++;
            j++;
        }

        return (left.Length - i).CompareTo(right.Length - j);
    }

    private static int CompareNumber(string left, ref int i, string right, ref int j)
    {
        var leftStart = i;
        var rightStart = j;
        while (i < left.Length && char.IsDigit(left[i])) i++;
        while (j < right.Length && char.IsDigit(right[j])) j++;

        var leftTrim = leftStart;
        var rightTrim = rightStart;
        while (leftTrim < i && left[leftTrim] == '0') leftTrim++;
        while (rightTrim < j && right[rightTrim] == '0') rightTrim++;

        var leftDigits = i - leftTrim;
        var rightDigits = j - rightTrim;
        if (leftDigits != rightDigits)
        {
            return leftDigits.CompareTo(rightDigits);
        }

        for (var offset = 0; offset < leftDigits; offset++)
        {
            var compare = left[leftTrim + offset].CompareTo(right[rightTrim + offset]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return (i - leftStart).CompareTo(j - rightStart);
    }
}
