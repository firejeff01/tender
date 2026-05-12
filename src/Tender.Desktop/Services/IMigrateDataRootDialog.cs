namespace Tender.Desktop.Services;

public enum MigrateChoice
{
    Cancel,
    Keep,
    Migrate,
}

public interface IMigrateDataRootDialog
{
    /// <summary>顯示資料搬移確認對話框，回傳使用者選擇。</summary>
    MigrateChoice Ask(string oldRoot, string newRoot);
}
