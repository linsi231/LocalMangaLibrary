using System.IO;

namespace LocalMangaLibrary;

public sealed class CsvStructureService
{
    private static readonly HashSet<string> RequiredColumns = new()
    {
        "文件夹名称",
        "相对路径",
        "直属文件数量",
        "包含子目录文件总数",
        "直属子文件夹数量",
    };

    public List<CsvFolderRow> Read(string csvPath)
    {
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length == 0)
        {
            return [];
        }

        var headers = ParseCsvLine(lines[0]).Select(h => h.Trim('\ufeff')).ToArray();
        var headerMap = headers.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index);
        var missing = RequiredColumns.Where(column => !headerMap.ContainsKey(column)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException("CSV 缺少必要列：" + string.Join(", ", missing));
        }

        var rows = new List<CsvFolderRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var values = ParseCsvLine(lines[i]).ToArray();
            string Get(string column)
            {
                var index = headerMap[column];
                return index < values.Length ? values[index].Trim() : "";
            }

            var relativePath = Get("相对路径").Trim('"');
            rows.Add(new CsvFolderRow
            {
                Index = i - 1,
                FolderName = Get("文件夹名称"),
                RelativePath = relativePath,
                Parts = relativePath.Replace('/', '\\').Trim('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries),
                DirectFileCount = ParseInt(Get("直属文件数量")),
                TotalFileCount = ParseInt(Get("包含子目录文件总数")),
                ChildFolderCount = ParseInt(Get("直属子文件夹数量")),
            });
        }

        return rows;
    }

    public List<CsvFolderRow> SelectWorkRows(List<CsvFolderRow> rows)
    {
        var candidates = rows
            .Where(IsWorkCandidate)
            .OrderBy(row => row.Parts.Length)
            .ThenBy(row => row.Index)
            .ToList();
        var selected = new List<CsvFolderRow>();
        foreach (var row in candidates)
        {
            if (row.Parts.Length == 0)
            {
                continue;
            }

            if (selected.Any(parent => IsAncestor(parent.Parts, row.Parts)))
            {
                continue;
            }

            selected.Add(row);
        }

        return selected.OrderBy(row => row.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsWorkCandidate(CsvFolderRow row)
    {
        if (row.TotalFileCount <= 0)
        {
            return false;
        }

        if (row.Depth == 0 && row.DirectFileCount == 0 && row.ChildFolderCount >= 12)
        {
            return false;
        }

        if (row.DirectFileCount > 0)
        {
            return true;
        }

        return row.Depth > 0 && row.ChildFolderCount is > 0 and <= 12;
    }

    private static bool IsAncestor(string[] parent, string[] child)
    {
        return parent.Length < child.Length && parent.SequenceEqual(child.Take(parent.Length));
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    private static IEnumerable<string> ParseCsvLine(string line)
    {
        var current = new List<char>();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Add('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                yield return new string(current.ToArray());
                current.Clear();
            }
            else
            {
                current.Add(ch);
            }
        }

        yield return new string(current.ToArray());
    }
}
