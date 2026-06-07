using System.IO;

namespace LocalMangaLibrary;

public sealed class LibraryScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif",
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

        return Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, NaturalPathComparer.Instance)
            .ToList();
    }

    public WorkItem ScanWork(string folderPath, bool generateThumbnail)
    {
        var dir = new DirectoryInfo(folderPath);
        var fileCount = 0;
        var imageCount = 0;
        long totalSize = 0;
        var lastModified = dir.Exists ? dir.LastWriteTime : DateTime.MinValue;
        var imagePaths = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
        {
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
                // Ignore inaccessible files; browsing should continue.
            }
        }

        imagePaths = imagePaths.OrderBy(path => path, NaturalPathComparer.Instance).ToList();
        var firstImage = imagePaths.FirstOrDefault() ?? "";
        var coverImage = PickCover(imagePaths);
        var thumbName = generateThumbnail && !string.IsNullOrEmpty(coverImage) ? _imageCache.MakeThumbnail(coverImage) : "";

        return new WorkItem
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
        };
    }

    public List<(string Folder, Dictionary<string, object?> Metadata)> DirectTargets(string rootPath)
    {
        return Directory.EnumerateDirectories(rootPath)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => (path, new Dictionary<string, object?>()))
            .ToList();
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
