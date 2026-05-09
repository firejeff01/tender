namespace Tender.Desktop.Services;

public sealed record CleanupResult(int RemovedFolders, int FailedFolders, IReadOnlyList<string> RemovedDates);

public interface IDataCleanupService
{
    /// <summary>
    /// 依 AppSettings.DataRetentionMonths 清理舊資料夾。
    /// 0 = 不清理。
    /// </summary>
    Task<CleanupResult> CleanupAsync(CancellationToken ct = default);
}
