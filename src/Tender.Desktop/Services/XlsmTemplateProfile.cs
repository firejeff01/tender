using Tender.Core.Models;

namespace Tender.Desktop.Services;

/// <summary>
/// 每一份 .xlsm 範本（ISMS / ESG 等）有自己的 cellXfs 樣式表與 worksheet rel id，
/// 此類保存「重寫 sheet1 / sheet2 時需要用到」的全部變數，避免每個匯出器重複寫一遍。
///
/// 樣式索引必須對應目標 .xlsm 的 styles.xml 中既有 cellXfs，否則開檔會出現「已修復的記錄」或樣式錯亂。
/// </summary>
internal sealed class XlsmTemplateProfile
{
    public required string TemplateRelativePath { get; init; }

    // ---- sheet1「全部資料」----
    public required string Sheet1SheetFormatPr { get; init; }
    public required string Sheet1Cols { get; init; }
    public required string Sheet1DrawingRelId { get; init; }
    public required string Sheet1PrinterRelId { get; init; }
    public required int Sheet1HeaderStyle { get; init; }
    public required int Sheet1HeaderJStyle { get; init; }
    public required int Sheet1HeaderHStyle { get; init; }
    public required int Sheet1DataBStyle { get; init; }
    public required int Sheet1DataCStyle { get; init; }
    public required int Sheet1DataDStyle { get; init; }
    public required int Sheet1DataEStyle { get; init; }
    public required int Sheet1DataFStyle { get; init; }
    public required int Sheet1DataGStyle { get; init; }
    public required int Sheet1DataHStyle { get; init; }
    public required int Sheet1DataIStyle { get; init; }
    public required int Sheet1DataJStyle { get; init; }

    // ---- sheet2「篩選」----
    public required string Sheet2DrawingRelId { get; init; }
    public required string Sheet2PrinterRelId { get; init; }
    public required int Sheet2ColumnStyle { get; init; }
    public required int Sheet2DataBStyle { get; init; }

    // ---- 1150511_ISMS標案.xlsm ----
    // styles.xml: cellXfs count=18；
    // sheet1.xml.rels: 印表 rId1637 / 繪圖 rId1638；
    // sheet2.xml.rels: 印表 rId1 / 繪圖 rId2。
    public static XlsmTemplateProfile Isms { get; } = new()
    {
        TemplateRelativePath = "Templates/tender-template-isms.xlsm",
        Sheet1SheetFormatPr = "<sheetFormatPr defaultRowHeight=\"16.5\"/>",
        Sheet1Cols =
            "<cols>" +
            "<col min=\"1\" max=\"1\" width=\"2.625\" style=\"6\" customWidth=\"1\"/>" +
            "<col min=\"2\" max=\"2\" width=\"45.5\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"3\" max=\"3\" width=\"9\" style=\"6\"/>" +
            "<col min=\"4\" max=\"4\" width=\"10.5\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"5\" max=\"5\" width=\"9.625\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"6\" max=\"7\" width=\"10\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"8\" max=\"8\" width=\"9.875\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"9\" max=\"9\" width=\"10.125\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"10\" max=\"16384\" width=\"9\" style=\"1\"/>" +
            "</cols>",
        Sheet1DrawingRelId = "rId1638",
        Sheet1PrinterRelId = "rId1637",
        Sheet1HeaderStyle = 9,
        Sheet1HeaderJStyle = 10,
        Sheet1HeaderHStyle = 9,
        Sheet1DataBStyle = 17,
        Sheet1DataCStyle = 12,
        Sheet1DataDStyle = 13,
        Sheet1DataEStyle = 14,
        Sheet1DataFStyle = 14,
        Sheet1DataGStyle = 14,
        Sheet1DataHStyle = 15,
        Sheet1DataIStyle = 13,
        Sheet1DataJStyle = 16,
        Sheet2DrawingRelId = "rId2",
        Sheet2PrinterRelId = "rId1",
        Sheet2ColumnStyle = 8,
        Sheet2DataBStyle = 1,
    };

    // ---- 1150511_ESG標案.xlsm ----
    // styles.xml: cellXfs count=25；
    // sheet1.xml.rels: 印表 rId1637 / 繪圖 rId1638（與 ISMS 相同）；
    // sheet2.xml.rels: 印表 rId3 / 繪圖 rId4。
    public static XlsmTemplateProfile Esg { get; } = new()
    {
        TemplateRelativePath = "Templates/tender-template-esg.xlsm",
        Sheet1SheetFormatPr = "<sheetFormatPr defaultRowHeight=\"16.5\"/>",
        Sheet1Cols =
            "<cols>" +
            "<col min=\"1\" max=\"1\" width=\"2.625\" style=\"10\" customWidth=\"1\"/>" +
            "<col min=\"2\" max=\"2\" width=\"45.5\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"3\" max=\"3\" width=\"9\" style=\"10\"/>" +
            "<col min=\"4\" max=\"4\" width=\"10.5\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"5\" max=\"5\" width=\"9.625\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"6\" max=\"7\" width=\"10\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"8\" max=\"8\" width=\"9.875\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"9\" max=\"9\" width=\"10.125\" style=\"1\" customWidth=\"1\"/>" +
            "<col min=\"10\" max=\"16384\" width=\"9\" style=\"1\"/>" +
            "</cols>",
        Sheet1DrawingRelId = "rId1638",
        Sheet1PrinterRelId = "rId1637",
        Sheet1HeaderStyle = 14,
        Sheet1HeaderJStyle = 15,
        Sheet1HeaderHStyle = 14,
        Sheet1DataBStyle = 24,
        Sheet1DataCStyle = 6,
        Sheet1DataDStyle = 7,
        Sheet1DataEStyle = 9,
        Sheet1DataFStyle = 9,
        Sheet1DataGStyle = 9,
        Sheet1DataHStyle = 8,
        Sheet1DataIStyle = 7,
        Sheet1DataJStyle = 16,
        Sheet2DrawingRelId = "rId4",
        Sheet2PrinterRelId = "rId3",
        Sheet2ColumnStyle = 12,
        Sheet2DataBStyle = 24,
    };
}

internal sealed class IsmsTemplateXlsmExporter : IIsmsXlsmExporter
{
    private readonly ProfiledTemplateXlsmExporter _inner = new(XlsmTemplateProfile.Isms);

    public Task ExportAsync(
        IReadOnlyList<TenderItem> allItems,
        IReadOnlyList<TenderItem> filteredItems,
        string savePath,
        CancellationToken ct = default)
        => _inner.ExportAsync(allItems, filteredItems, savePath, ct);
}

internal sealed class EsgTemplateXlsmExporter : IEsgXlsmExporter
{
    private readonly ProfiledTemplateXlsmExporter _inner = new(XlsmTemplateProfile.Esg);

    public Task ExportAsync(
        IReadOnlyList<TenderItem> allItems,
        IReadOnlyList<TenderItem> filteredItems,
        string savePath,
        CancellationToken ct = default)
        => _inner.ExportAsync(allItems, filteredItems, savePath, ct);
}
