using Tender.Core.Models;

namespace Tender.Desktop.Services;

public interface IXlsmExporter
{
    /// <summary>
    /// 匯出 .xlsm（含 1150508 範本的 VBA 巨集與 88 個關鍵字按鈕）；三個工作表：
    ///   sheet1「全部資料」：allItems。9 欄 B–J（B 標案 link / C 案號 / D 招標方式 / E 採購性質
    ///                       / F 公告 / G 截止 / H 預算 / I 機關 / J 檢視 link）。
    ///   sheet2「篩選」    ：filteredItems。B 欄一格放「機關名稱:標案名稱」並帶 link。
    ///   sheet3「工作表1」：allItems。3 欄：A 機關 / B 標案 link / C 機關:標案 link。
    /// </summary>
    Task ExportAsync(
        IReadOnlyList<TenderItem> allItems,
        IReadOnlyList<TenderItem> filteredItems,
        string savePath,
        CancellationToken ct = default);
}
