using System.Globalization;

namespace LocalMangaLibrary;

public static class FormatService
{
    public static string NowIso()
    {
        return DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    public static string FormatBytes(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)size;
        foreach (var unit in units)
        {
            if (value < 1024 || unit == units[^1])
            {
                return unit == "B" ? $"{(long)value} {unit}" : $"{value:F1} {unit}";
            }

            value /= 1024;
        }

        return $"{size} B";
    }
}
