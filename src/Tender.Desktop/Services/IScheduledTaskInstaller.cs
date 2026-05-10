namespace Tender.Desktop.Services;

public interface IScheduledTaskInstaller
{
    /// <summary>
    /// 以使用者層級建立或覆寫 TenderSearch.DailyCrawl 排程任務。
    /// 不需 admin 權限：未指定 /RU 時 schtasks 會以呼叫者身份建立。
    /// schtasks /F 為 idempotent，重複執行只會覆蓋為新時間。
    /// </summary>
    /// <param name="scheduledTime">HH:mm 格式（例：17:00）。</param>
    /// <returns>true 為成功；false 為找不到 crawler exe 或 schtasks 失敗。</returns>
    bool EnsureTask(string scheduledTime);
}
