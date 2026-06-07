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

    public string AppDataDir { get; }
    public string DataDir { get; }
    public string CacheDir { get; }
    public string ThumbDir { get; }
    public string ReaderDir { get; }
    public string ConfigPath => Path.Combine(DataDir, "config.json");
    public string IndexPath => Path.Combine(DataDir, "library_index.json");
    public string DecisionsPath => Path.Combine(DataDir, "decisions.json");
    public string RecentPath => Path.Combine(DataDir, "recent.json");

    public StorageService()
    {
        AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalMangaLibrary");
        DataDir = Path.Combine(AppDataDir, "data");
        CacheDir = Path.Combine(AppContext.BaseDirectory, ".cache");
        ThumbDir = Path.Combine(CacheDir, "thumbs");
        ReaderDir = Path.Combine(CacheDir, "reader");
        Ensure();
    }

    public void Ensure()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ThumbDir);
        Directory.CreateDirectory(ReaderDir);
        if (!File.Exists(ConfigPath))
        {
            SaveJson(ConfigPath, new LibraryConfig());
        }

        if (!File.Exists(IndexPath))
        {
            SaveJson(IndexPath, new LibraryIndex());
        }

        if (!File.Exists(DecisionsPath))
        {
            SaveJson(DecisionsPath, new List<DecisionItem>());
        }

        if (!File.Exists(RecentPath))
        {
            SaveJson(RecentPath, new List<RecentItem>());
        }
    }

    public T LoadJson<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path))
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
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(temp, path);
    }

    public void ClearCache()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (TryDeleteDirectory(CacheDir) || !Directory.Exists(CacheDir))
            {
                return;
            }

            Thread.Sleep(120);
        }

        TryDeleteRemainingChildren(CacheDir);
        TryDeleteDirectory(CacheDir);
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return true;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { }
            }

            Directory.Delete(path, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteRemainingChildren(string path)
    {
        try
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
                TryDeleteDirectory(dir);
            }
        }
        catch { }
    }
}
