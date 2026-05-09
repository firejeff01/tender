using System.IO;
using Tender.Core.Clock;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Desktop.Services;

public sealed class DataCleanupService : IDataCleanupService
{
    private readonly IDataPaths _paths;
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IClock _clock;

    public DataCleanupService(IDataPaths paths, IAppSettingsRepository settingsRepo, IClock clock)
    {
        _paths = paths;
        _settingsRepo = settingsRepo;
        _clock = clock;
    }

    public async Task<CleanupResult> CleanupAsync(CancellationToken ct = default)
    {
        var settings = await _settingsRepo.LoadAsync(ct);
        if (settings.DataRetentionMonths <= 0)
            return new CleanupResult(0, 0, Array.Empty<string>());

        var cutoff = DateOnly.FromDateTime(_clock.Now.LocalDateTime.AddMonths(-settings.DataRetentionMonths));
        var removed = new List<string>();
        var failed = 0;

        // 掃描 data/yyyy-MM/yyyy-MM-dd/
        if (!Directory.Exists(_paths.DataRoot))
            return new CleanupResult(0, 0, Array.Empty<string>());

        foreach (var monthDir in Directory.EnumerateDirectories(_paths.DataRoot))
        {
            ct.ThrowIfCancellationRequested();
            var monthName = Path.GetFileName(monthDir);

            // 跳過非 yyyy-MM 命名（例如 settings/、locks/）
            if (!System.Text.RegularExpressions.Regex.IsMatch(monthName, @"^\d{4}-\d{2}$"))
                continue;

            foreach (var dayDir in Directory.EnumerateDirectories(monthDir))
            {
                var dayName = Path.GetFileName(dayDir);
                if (!DateOnly.TryParseExact(dayName, "yyyy-MM-dd", out var date))
                    continue;

                if (date < cutoff)
                {
                    try
                    {
                        Directory.Delete(dayDir, recursive: true);
                        removed.Add(dayName);
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }

            // 月份資料夾若已空也清掉
            try
            {
                if (Directory.Exists(monthDir) && !Directory.EnumerateFileSystemEntries(monthDir).Any())
                    Directory.Delete(monthDir);
            }
            catch { /* ignore */ }
        }

        return new CleanupResult(removed.Count, failed, removed.AsReadOnly());
    }
}
