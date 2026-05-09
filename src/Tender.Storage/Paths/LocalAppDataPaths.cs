namespace Tender.Storage.Paths;

/// <summary>
/// 正式環境路徑實作，資料根目錄為 %LocalAppData%/TenderSearch/data/。
/// 可透過建構子傳入自訂 root（測試用）。
/// </summary>
public sealed class LocalAppDataPaths : IDataPaths
{
    public string DataRoot { get; }

    public LocalAppDataPaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TenderSearch",
            "data"))
    { }

    public LocalAppDataPaths(string dataRoot)
    {
        DataRoot = dataRoot;
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
}
