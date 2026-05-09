using Tender.Core.Models;

namespace Tender.Desktop.Services;

public interface IExcelExporter
{
    /// <summary>
    /// 匯出標案集合為 .xlsx。
    /// 表頭欄位：標案名稱、招標方式、採購性質、公告日期、截止投標、預算金額、機關名稱、檢視連結、機關名稱：標案名稱、命中關鍵字。
    /// 「標案名稱」「檢視連結」「機關名稱：標案名稱」欄位寫入 hyperlink。
    /// 預算金額為 number、日期為 text。
    /// </summary>
    Task ExportAsync(
        IReadOnlyList<TenderItem> items,
        string savePath,
        CancellationToken ct = default);
}
