namespace Tender.Storage.Paths;

/// <summary>
/// 正式環境路徑實作，資料根目錄為 %LocalAppData%/TenderSearch/data/。
/// 可透過建構子傳入自訂 root（測試用）。
/// </summary>
public sealed class LocalAppDataPaths : IDataPaths
{
    private string _dataRoot;

    public string DataRoot => _dataRoot;

    public string DefaultDataRoot => GetDefaultDataRoot();

    public event Action<string>? DataRootChanged;

    public LocalAppDataPaths()
    {
        _dataRoot = ReadBootstrapRoot() ?? GetDefaultDataRoot();
    }

    public LocalAppDataPaths(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public void ChangeRoot(string? newRoot)
    {
        var resolved = string.IsNullOrWhiteSpace(newRoot)
            ? GetDefaultDataRoot()
            : Path.GetFullPath(newRoot);

        if (string.Equals(resolved, _dataRoot, StringComparison.OrdinalIgnoreCase))
            return;

        WriteBootstrapRoot(resolved);
        _dataRoot = resolved;
        DataRootChanged?.Invoke(resolved);
    }

    public string DailyFolder(DateOnly date)
        => Path.Combine(DataRoot, date.ToString("yyyy-MM"), date.ToString("yyyy-MM-dd"));

    public string TendersFile(DateOnly date)
        => Path.Combine(DailyFolder(date), "tenders.json");

    public string SummaryFile(DateOnly date)
        => Path.Combine(DailyFolder(date), "summary.json");

    public string CrawlRunsFile(DateOnly date)
        => Path.Combine(DailyFolder(date), "crawl-runs.json");

    public string ErrorsLogFile(DateOnly date)
        => Path.Combine(DailyFolder(date), "errors.log");

    public string KeywordsFile
        => Path.Combine(DataRoot, "settings", "keywords.json");

    public string UserMarksFile
        => Path.Combine(DataRoot, "settings", "user-marks.json");

    public string AppSettingsFile
        => Path.Combine(DataRoot, "settings", "app-settings.json");

    public string SavedSearchesFile
        => Path.Combine(DataRoot, "settings", "saved-searches.json");

    public string CrawlerLockFile
        => Path.Combine(DataRoot, "locks", "crawler.lock");

    public IReadOnlyList<DateOnly> ListDaysWithData(int year, int month)
    {
        var monthFolder = Path.Combine(DataRoot, $"{year:D4}-{month:D2}");
        if (!Directory.Exists(monthFolder))
            return Array.Empty<DateOnly>();

        var result = new List<DateOnly>();
        foreach (var dir in Directory.GetDirectories(monthFolder))
        {
            var dirName = Path.GetFileName(dir);
            if (DateOnly.TryParseExact(dirName, "yyyy-MM-dd", out var date))
                result.Add(date);
        }

        result.Sort();
        return result.AsReadOnly();
    }

    private static string GetDefaultDataRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TenderSearch",
            "data");

    /// <summary>
    /// bootstrap 設定檔位置：%LocalAppData%/TenderSearch/dataroot.txt
    /// 必須放在 DataRoot 外，否則無法在啟動時得知該讀哪個 DataRoot。
    /// </summary>
    private static string GetBootstrapFile()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TenderSearch",
            "dataroot.txt");

    private static string? ReadBootstrapRoot()
    {
        try
        {
            var path = GetBootstrapFile();
            if (!File.Exists(path)) return null;
            var content = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteBootstrapRoot(string root)
    {
        var path = GetBootstrapFile();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, root);
    }
}
