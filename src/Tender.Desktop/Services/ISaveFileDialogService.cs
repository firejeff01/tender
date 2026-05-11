namespace Tender.Desktop.Services;

public interface ISaveFileDialogService
{
    /// <summary>
    /// 顯示另存新檔對話框（xlsx），回傳使用者選擇的路徑；取消則回傳 null。
    /// </summary>
    string? ShowSaveAsXlsx(string suggestedFileName);

    /// <summary>
    /// 顯示另存新檔對話框（xlsm，含巨集），回傳使用者選擇的路徑；取消則回傳 null。
    /// </summary>
    string? ShowSaveAsXlsm(string suggestedFileName);
}
