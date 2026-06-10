using System.IO;

namespace LocalMangaLibrary;

public sealed class RuntimeStateService
{
    private readonly object _lock = new();

    public LibraryConfig Config { get; private set; } = new();
    public LibraryIndex CurrentIndex { get; private set; } = new();
    public List<DecisionItem> Decisions { get; private set; } = [];
    public List<DeleteLogItem> DeleteLogs { get; private set; } = [];
    public List<RecentItem> Recent { get; private set; } = [];
    public List<RootHistoryItem> RootHistory { get; private set; } = [];
    public LibraryUiState LibraryUiState { get; private set; } = new();
    public Guid RootVersion { get; private set; } = Guid.NewGuid();

    public RuntimeSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new RuntimeSnapshot
            {
                Config = CloneConfig(Config),
                CurrentIndex = CloneIndex(CurrentIndex),
                Decisions = Decisions.ToList(),
                DeleteLogs = DeleteLogs.ToList(),
                Recent = Recent.ToList(),
                RootHistory = RootHistory.ToList(),
                LibraryUiState = CloneLibraryUiState(LibraryUiState),
                RootVersion = RootVersion,
            };
        }
    }

    public Guid ReplaceRoot(string rootPath)
    {
        lock (_lock)
        {
            RootVersion = Guid.NewGuid();
            Config = new LibraryConfig
            {
                RootPath = rootPath,
                Theme = "blue",
                LibraryViewMode = "grid",
                UpdatedAt = FormatService.NowIso(),
            };
            CurrentIndex = new LibraryIndex { RootPath = rootPath, ScanSource = "empty" };
            Decisions = [];
            DeleteLogs = [];
            LibraryUiState = NewLibraryUiState(RootVersion);
            return RootVersion;
        }
    }

    public Guid BeginRescan(string rootPath)
    {
        lock (_lock)
        {
            RootVersion = Guid.NewGuid();
            Config.RootPath = rootPath;
            Config.UpdatedAt = FormatService.NowIso();
            CurrentIndex = new LibraryIndex { RootPath = rootPath, ScanSource = "scanning" };
            Decisions = [];
            DeleteLogs = [];
            LibraryUiState = NewLibraryUiState(RootVersion);
            return RootVersion;
        }
    }

    public bool IsCurrent(Guid version, string rootPath)
    {
        lock (_lock)
        {
            return RootVersion == version && SamePath(Config.RootPath, rootPath);
        }
    }

    public void SetIndex(Guid version, LibraryIndex index)
    {
        lock (_lock)
        {
            if (RootVersion != version || !SamePath(Config.RootPath, index.RootPath))
            {
                return;
            }

            CurrentIndex = CloneIndex(index);
        }
    }

    public void SetTheme(string theme)
    {
        lock (_lock)
        {
            Config.Theme = theme;
            Config.UpdatedAt = FormatService.NowIso();
        }
    }

    public void SetLibraryViewMode(string mode)
    {
        lock (_lock)
        {
            Config.LibraryViewMode = mode;
            Config.UpdatedAt = FormatService.NowIso();
        }
    }

    public void SetDecisions(List<DecisionItem> decisions)
    {
        lock (_lock)
        {
            Decisions = decisions.ToList();
        }
    }

    public void SetDeleteLogs(List<DeleteLogItem> logs)
    {
        lock (_lock)
        {
            DeleteLogs = logs.ToList();
        }
    }

    public void SetRecent(List<RecentItem> recent)
    {
        lock (_lock)
        {
            Recent = recent.ToList();
        }
    }

    public void SetRootHistory(List<RootHistoryItem> history)
    {
        lock (_lock)
        {
            RootHistory = history.ToList();
        }
    }

    public void SetLibraryUiState(LibraryUiState uiState)
    {
        lock (_lock)
        {
            if (!Guid.TryParse(uiState.RootVersion, out var version) || version != RootVersion)
            {
                return;
            }

            LibraryUiState = CloneLibraryUiState(uiState);
        }
    }

    public void ClearRecent()
    {
        lock (_lock)
        {
            Recent = [];
        }
    }

    public void ClearRootHistory()
    {
        lock (_lock)
        {
            RootHistory = [];
        }
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            Recent = [];
            RootHistory = [];
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            Config = new LibraryConfig();
            CurrentIndex = new LibraryIndex();
            Decisions = [];
            DeleteLogs = [];
            Recent = [];
            RootHistory = [];
            LibraryUiState = new LibraryUiState();
            RootVersion = Guid.NewGuid();
        }
    }

    private static LibraryUiState NewLibraryUiState(Guid rootVersion) => new()
    {
        RootVersion = rootVersion.ToString("N"),
    };

    private static LibraryConfig CloneConfig(LibraryConfig source) => new()
    {
        RootPath = source.RootPath,
        StructureCsvPath = source.StructureCsvPath,
        Theme = string.IsNullOrWhiteSpace(source.Theme) ? "blue" : source.Theme,
        LibraryViewMode = string.IsNullOrWhiteSpace(source.LibraryViewMode) ? "grid" : source.LibraryViewMode,
        UpdatedAt = source.UpdatedAt,
    };

    private static LibraryIndex CloneIndex(LibraryIndex source) => new()
    {
        RootPath = source.RootPath,
        LastScanned = source.LastScanned,
        ItemCount = source.ItemCount,
        TotalImageCount = source.TotalImageCount,
        ScannedDirectoryCount = source.ScannedDirectoryCount,
        SkippedDirectoryCount = source.SkippedDirectoryCount,
        ErrorCount = source.ErrorCount,
        ErrorsSample = source.ErrorsSample.ToList(),
        Items = source.Items.ToList(),
        ScanSource = source.ScanSource,
        StructureCsvPath = source.StructureCsvPath,
        StructureCsvRows = source.StructureCsvRows,
        StructureCsvCandidates = source.StructureCsvCandidates,
        MissingPathCount = source.MissingPathCount,
        MissingPathsSample = source.MissingPathsSample?.ToList(),
        CsvWarning = source.CsvWarning,
        CsvAttempt = source.CsvAttempt,
    };

    private static LibraryUiState CloneLibraryUiState(LibraryUiState source) => new()
    {
        SelectedWorkPath = source.SelectedWorkPath,
        SelectedWorkIndex = source.SelectedWorkIndex,
        ScrollOffset = source.ScrollOffset,
        CurrentPage = source.CurrentPage,
        SearchQuery = source.SearchQuery,
        SortMode = source.SortMode,
        FilterMode = source.FilterMode,
        ViewMode = source.ViewMode,
        RootVersion = source.RootVersion,
    };

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
}

public sealed class RuntimeSnapshot
{
    public LibraryConfig Config { get; set; } = new();
    public LibraryIndex CurrentIndex { get; set; } = new();
    public List<DecisionItem> Decisions { get; set; } = [];
    public List<DeleteLogItem> DeleteLogs { get; set; } = [];
    public List<RecentItem> Recent { get; set; } = [];
    public List<RootHistoryItem> RootHistory { get; set; } = [];
    public LibraryUiState LibraryUiState { get; set; } = new();
    public Guid RootVersion { get; set; }
}
