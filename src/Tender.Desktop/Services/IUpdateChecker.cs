namespace Tender.Desktop.Services;

public sealed record UpdateInfo(
    Version CurrentVersion,
    Version? LatestVersion,
    bool IsUpdateAvailable,
    string? DownloadUrl,
    string? ReleaseNotes,
    string? ReleasePageUrl);

public interface IUpdateChecker
{
    /// <summary>
    /// 對 GitHub Releases 查詢最新版本，與當前執行版本比對。
    /// 失敗（網路、API 限制）回傳 IsUpdateAvailable = false 不丟例外。
    /// </summary>
    Task<UpdateInfo> CheckAsync(CancellationToken ct = default);
}
