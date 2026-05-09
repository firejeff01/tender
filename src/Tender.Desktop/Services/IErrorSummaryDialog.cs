namespace Tender.Desktop.Services;

public interface IErrorSummaryDialog
{
    /// <summary>
    /// 顯示錯誤摘要視窗，內容來自 summary.json 的 errorMessage + 該日 errors.log 內容。
    /// </summary>
    Task ShowAsync(DateOnly date, string errorMessage, string errorsLogPath);
}
