using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Tender.Core.Models;

namespace Tender.Desktop.Services;

/// <summary>
/// 以「Templates/tender-template.xlsm」為基底匯出 .xlsm：
///   1. 複製範本到目標路徑（保留 VBA、88 個關鍵字按鈕與其它形狀）
///   2. 用 ZipArchive 重寫 3 個工作表 + 各自的 _rels：
///        sheet1「全部資料」：allItems（9 欄 B-J，含 hyperlink）
///        sheet2「篩選」    ：filteredItems（B 欄 機關:標案 + hyperlink）
///        sheet3「工作表1」：allItems（A 機關 / B 標案 link / C 機關:標案 link）
///
/// 不動 vbaProject.bin、drawings、styles.xml、sharedStrings.xml，巨集功能維持原樣。
/// 所有字串用 inlineStr，不必動 sharedStrings.xml。
/// </summary>
public sealed class TemplateXlsmExporter : IXlsmExporter
{
    private const string TemplateRelativePath = "Templates/tender-template.xlsm";
    private const string Sheet1Path = "xl/worksheets/sheet1.xml";
    private const string Sheet1RelsPath = "xl/worksheets/_rels/sheet1.xml.rels";
    private const string Sheet2Path = "xl/worksheets/sheet2.xml";
    private const string Sheet2RelsPath = "xl/worksheets/_rels/sheet2.xml.rels";
    private const string Sheet3Path = "xl/worksheets/sheet3.xml";
    private const string Sheet3RelsPath = "xl/worksheets/_rels/sheet3.xml.rels";

    // 範本內固定 rel id
    private const string S1DrawingRelId    = "rId2668";
    private const string S1PrinterRelId    = "rId2667";
    private const string S2DrawingRelId    = "rId54";
    private const string S2PrinterRelId    = "rId53";
    private const string S3DrawingRelId    = "rId2667";

    public Task ExportAsync(
        IReadOnlyList<TenderItem> allItems,
        IReadOnlyList<TenderItem> filteredItems,
        string savePath,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var templatePath = ResolveTemplatePath();
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"找不到 xlsm 範本：{templatePath}");

            File.Copy(templatePath, savePath, overwrite: true);

            using var zip = ZipFile.Open(savePath, ZipArchiveMode.Update);
            ReplaceEntry(zip, Sheet1Path,     BuildSheet1Xml(allItems, ct));
            ReplaceEntry(zip, Sheet1RelsPath, BuildSheet1Rels(allItems));
            ReplaceEntry(zip, Sheet2Path,     BuildSheet2Xml(filteredItems, ct));
            ReplaceEntry(zip, Sheet2RelsPath, BuildSheet2Rels(filteredItems));
            ReplaceEntry(zip, Sheet3Path,     BuildSheet3Xml(allItems, ct));
            ReplaceEntry(zip, Sheet3RelsPath, BuildSheet3Rels(allItems));
        }, ct);
    }

    private static string ResolveTemplatePath()
    {
        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                      ?? AppContext.BaseDirectory;
        return Path.Combine(baseDir, TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ReplaceEntry(ZipArchive zip, string entryName, string content)
    {
        var existing = zip.GetEntry(entryName);
        existing?.Delete();

        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var sw = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.Write(content);
    }

    // =================================================================
    // sheet1「全部資料」：allItems，欄位 B-J
    // =================================================================
    private static string BuildSheet1Xml(IReadOnlyList<TenderItem> items, CancellationToken ct)
    {
        int lastRow = 2 + items.Count;
        var lastRef = $"J{Math.Max(lastRow, 2)}";

        var sb = new StringBuilder(8 * 1024 + items.Count * 320);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"");
        sb.Append(" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"");
        sb.Append(" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
        sb.Append(" mc:Ignorable=\"x14ac xr xr2 xr3\"");
        sb.Append(" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\"");
        sb.Append(" xmlns:xr=\"http://schemas.microsoft.com/office/spreadsheetml/2014/revision\"");
        sb.Append(" xmlns:xr2=\"http://schemas.microsoft.com/office/spreadsheetml/2015/revision2\"");
        sb.Append(" xmlns:xr3=\"http://schemas.microsoft.com/office/spreadsheetml/2016/revision3\"");
        sb.Append(" xr:uid=\"{00000000-0001-0000-0000-000000000000}\">");
        sb.Append("<sheetPr codeName=\"Sheet1\"/>");
        sb.Append($"<dimension ref=\"B1:{lastRef}\"/>");
        sb.Append("<sheetViews><sheetView tabSelected=\"1\" zoomScaleNormal=\"100\" workbookViewId=\"0\">");
        // state="frozen" 的 pane 需要 topLeftCell；缺它 Excel 開啟時會跳「已修復的記錄」
        sb.Append("<pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        sb.Append("<selection pane=\"bottomLeft\" activeCell=\"B3\" sqref=\"B3\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultColWidth=\"9\" defaultRowHeight=\"16.149999999999999\"/>");
        sb.Append("<cols>");
        sb.Append("<col min=\"1\" max=\"1\" width=\"2.59765625\" style=\"3\" customWidth=\"1\"/>");
        sb.Append("<col min=\"2\" max=\"2\" width=\"45.46484375\" customWidth=\"1\"/>");
        sb.Append("<col min=\"3\" max=\"3\" width=\"9\" style=\"3\"/>");
        sb.Append("<col min=\"4\" max=\"4\" width=\"10.46484375\" customWidth=\"1\"/>");
        sb.Append("<col min=\"5\" max=\"5\" width=\"9.59765625\" customWidth=\"1\"/>");
        sb.Append("<col min=\"6\" max=\"7\" width=\"11.9296875\" bestFit=\"1\" customWidth=\"1\"/>");
        sb.Append("<col min=\"8\" max=\"8\" width=\"16.59765625\" style=\"6\" customWidth=\"1\"/>");
        sb.Append("<col min=\"9\" max=\"9\" width=\"10.1328125\" customWidth=\"1\"/>");
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        // Row 1：按鈕 banner；Row 2：欄位標頭（引用 sharedStrings index 0~6）
        // 原檔 row 2 hidden=1 是 autoFilter 過濾覆蓋造成的「視覺隱藏」；若我們也標 hidden=1，
        // 巨集 ApplyFilter 內 Cells.Find(LookIn:=xlValues) 會跳過隱藏列導致找不到「機關名稱」。
        // 解法：讓 row 2 不隱藏。標頭顯示對使用者也是有幫助。
        sb.Append("<row r=\"1\" spans=\"2:10\" ht=\"133.9\" customHeight=\"1\"/>");
        sb.Append("<row r=\"2\" spans=\"2:10\" ht=\"42\" customHeight=\"1\">");
        sb.Append("<c r=\"B2\" s=\"4\" t=\"s\"><v>2</v></c>");
        sb.Append("<c r=\"C2\" s=\"4\"/>");
        sb.Append("<c r=\"D2\" s=\"4\" t=\"s\"><v>1</v></c>");
        sb.Append("<c r=\"E2\" s=\"4\" t=\"s\"><v>3</v></c>");
        sb.Append("<c r=\"F2\" s=\"4\" t=\"s\"><v>4</v></c>");
        sb.Append("<c r=\"G2\" s=\"4\" t=\"s\"><v>5</v></c>");
        sb.Append("<c r=\"H2\" s=\"7\" t=\"s\"><v>6</v></c>");
        sb.Append("<c r=\"I2\" s=\"4\" t=\"s\"><v>0</v></c>");
        sb.Append("<c r=\"J2\" s=\"5\"/>");
        sb.Append("</row>");

        var hyperlinks = new StringBuilder();
        int hSeq = 0;
        int rowIdx = 3;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            sb.Append($"<row r=\"{rowIdx}\" spans=\"2:10\" ht=\"16.5\" customHeight=\"1\">");
            AppendInlineStr(sb, $"B{rowIdx}", 8,  item.TenderName);
            if (string.IsNullOrEmpty(item.TenderNo))
                sb.Append($"<c r=\"C{rowIdx}\" s=\"10\"/>");
            else
                AppendInlineStr(sb, $"C{rowIdx}", 10, item.TenderNo);
            AppendInlineStr(sb, $"D{rowIdx}", 2,  item.TenderMethod);
            AppendInlineStr(sb, $"E{rowIdx}", 12, item.ProcurementType);
            AppendInlineStr(sb, $"F{rowIdx}", 12, item.AnnouncementDate);
            AppendInlineStr(sb, $"G{rowIdx}", 12, item.BidDeadline);
            if (item.BudgetAmount.HasValue)
                sb.Append($"<c r=\"H{rowIdx}\" s=\"13\"><v>{item.BudgetAmount.Value}</v></c>");
            else
                sb.Append($"<c r=\"H{rowIdx}\" s=\"13\"/>");
            AppendInlineStr(sb, $"I{rowIdx}", 2,  item.AgencyName);
            AppendInlineStr(sb, $"J{rowIdx}", 14, "檢視");
            sb.Append("</row>");

            if (!string.IsNullOrWhiteSpace(item.DetailUrl))
            {
                hyperlinks.Append($"<hyperlink ref=\"B{rowIdx}\" r:id=\"rIdS1H{++hSeq}\"/>");
                hyperlinks.Append($"<hyperlink ref=\"J{rowIdx}\" r:id=\"rIdS1H{++hSeq}\"/>");
            }

            rowIdx++;
        }
        sb.Append("</sheetData>");

        // autoFilter：巨集（特別是 "機關名稱"、"水利署" 等代理篩選類）依賴 sheet 上已有 autoFilter
        //   定義範圍 + _FilterDatabase named range，缺它跑巨集會跳「找不到 機關名稱」。
        //   範圍對齊原檔 A1:I{lastRow}，含 row 1 banner、row 2 hidden header。
        if (items.Count > 0)
            sb.Append($"<autoFilter ref=\"A1:I{lastRow}\"/>");

        // 順序依 OOXML schema：phoneticPr → hyperlinks → pageMargins → pageSetup → headerFooter → drawing
        sb.Append("<phoneticPr fontId=\"2\" type=\"noConversion\"/>");
        if (hyperlinks.Length > 0)
            sb.Append("<hyperlinks>").Append(hyperlinks).Append("</hyperlinks>");
        sb.Append("<pageMargins left=\"0.75\" right=\"0.75\" top=\"1\" bottom=\"1\" header=\"0.5\" footer=\"0.5\"/>");
        sb.Append($"<pageSetup paperSize=\"9\" orientation=\"portrait\" r:id=\"{S1PrinterRelId}\"/>");
        sb.Append("<headerFooter alignWithMargins=\"0\"/>");
        sb.Append($"<drawing r:id=\"{S1DrawingRelId}\"/>");
        sb.Append("</worksheet>");

        return sb.ToString();
    }

    private static string BuildSheet1Rels(IReadOnlyList<TenderItem> items)
    {
        var sb = new StringBuilder(4 * 1024 + items.Count * 220);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        sb.Append($"<Relationship Id=\"{S1PrinterRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/printerSettings\" Target=\"../printerSettings/printerSettings1.bin\"/>");
        sb.Append($"<Relationship Id=\"{S1DrawingRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>");

        int hSeq = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.DetailUrl)) continue;
            var safe = EscapeXmlAttr(item.DetailUrl);
            sb.Append($"<Relationship Id=\"rIdS1H{++hSeq}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{safe}\" TargetMode=\"External\"/>");
            sb.Append($"<Relationship Id=\"rIdS1H{++hSeq}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{safe}\" TargetMode=\"External\"/>");
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // =================================================================
    // sheet2「篩選」：filteredItems，B 欄一格放「機關名稱:標案名稱」+ link
    // =================================================================
    private static string BuildSheet2Xml(IReadOnlyList<TenderItem> items, CancellationToken ct)
    {
        int lastRow = Math.Max(items.Count, 1);
        var sb = new StringBuilder(2 * 1024 + items.Count * 180);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"");
        sb.Append(" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"");
        sb.Append(" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
        sb.Append(" mc:Ignorable=\"x14ac xr xr2 xr3\"");
        sb.Append(" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\"");
        sb.Append(" xmlns:xr=\"http://schemas.microsoft.com/office/spreadsheetml/2014/revision\"");
        sb.Append(" xmlns:xr2=\"http://schemas.microsoft.com/office/spreadsheetml/2015/revision2\"");
        sb.Append(" xmlns:xr3=\"http://schemas.microsoft.com/office/spreadsheetml/2016/revision3\"");
        sb.Append(" xr:uid=\"{00000000-0001-0000-0100-000000000000}\">");
        sb.Append("<sheetPr codeName=\"Sheet2\"/>");
        sb.Append($"<dimension ref=\"A1:B{lastRow}\"/>");
        sb.Append("<sheetViews><sheetView topLeftCell=\"B1\" zoomScaleNormal=\"100\" workbookViewId=\"0\">");
        sb.Append("<selection activeCell=\"B1\" sqref=\"B1\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"27.75\" customHeight=\"1\"/>");
        sb.Append("<cols>");
        sb.Append("<col min=\"1\" max=\"1\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>");
        sb.Append("<col min=\"2\" max=\"2\" width=\"94.265625\" style=\"1\" bestFit=\"1\" customWidth=\"1\"/>");
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        var hyperlinks = new StringBuilder();
        int hSeq = 0;
        int rowIdx = 1;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var combined = $"{item.AgencyName}：{item.TenderName}";
            sb.Append($"<row r=\"{rowIdx}\" spans=\"1:2\">");
            AppendInlineStr(sb, $"B{rowIdx}", 8, combined);
            sb.Append("</row>");

            if (!string.IsNullOrWhiteSpace(item.DetailUrl))
                hyperlinks.Append($"<hyperlink ref=\"B{rowIdx}\" r:id=\"rIdS2H{++hSeq}\"/>");

            rowIdx++;
        }
        sb.Append("</sheetData>");

        sb.Append("<phoneticPr fontId=\"2\" type=\"noConversion\"/>");
        if (hyperlinks.Length > 0)
            sb.Append("<hyperlinks>").Append(hyperlinks).Append("</hyperlinks>");
        sb.Append("<pageMargins left=\"0.75\" right=\"0.75\" top=\"1\" bottom=\"1\" header=\"0.5\" footer=\"0.5\"/>");
        sb.Append($"<pageSetup paperSize=\"9\" orientation=\"portrait\" r:id=\"{S2PrinterRelId}\"/>");
        sb.Append("<headerFooter alignWithMargins=\"0\"/>");
        sb.Append($"<drawing r:id=\"{S2DrawingRelId}\"/>");
        sb.Append("</worksheet>");

        return sb.ToString();
    }

    private static string BuildSheet2Rels(IReadOnlyList<TenderItem> items)
    {
        var sb = new StringBuilder(2 * 1024 + items.Count * 200);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        sb.Append($"<Relationship Id=\"{S2PrinterRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/printerSettings\" Target=\"../printerSettings/printerSettings2.bin\"/>");
        sb.Append($"<Relationship Id=\"{S2DrawingRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing2.xml\"/>");

        int hSeq = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.DetailUrl)) continue;
            var safe = EscapeXmlAttr(item.DetailUrl);
            sb.Append($"<Relationship Id=\"rIdS2H{++hSeq}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{safe}\" TargetMode=\"External\"/>");
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // =================================================================
    // sheet3「工作表1」：allItems。A 機關 / B 標案 link / C 機關:標案 link
    // =================================================================
    private static string BuildSheet3Xml(IReadOnlyList<TenderItem> items, CancellationToken ct)
    {
        int lastRow = Math.Max(items.Count, 1);
        var sb = new StringBuilder(2 * 1024 + items.Count * 260);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"");
        sb.Append(" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"");
        sb.Append(" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
        sb.Append(" mc:Ignorable=\"x14ac xr xr2 xr3\"");
        sb.Append(" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\"");
        sb.Append(" xmlns:xr=\"http://schemas.microsoft.com/office/spreadsheetml/2014/revision\"");
        sb.Append(" xmlns:xr2=\"http://schemas.microsoft.com/office/spreadsheetml/2015/revision2\"");
        sb.Append(" xmlns:xr3=\"http://schemas.microsoft.com/office/spreadsheetml/2016/revision3\"");
        sb.Append(" xr:uid=\"{45ADB01E-7A97-43E0-85DB-13D1B05CD249}\">");
        sb.Append("<sheetPr codeName=\"工作表1\"/>");
        sb.Append($"<dimension ref=\"A1:C{lastRow}\"/>");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
        sb.Append("<selection activeCell=\"A1\" sqref=\"A1\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"16.149999999999999\"/>");
        sb.Append("<cols>");
        sb.Append("<col min=\"1\" max=\"1\" width=\"22\" customWidth=\"1\"/>");
        sb.Append("<col min=\"2\" max=\"2\" width=\"45.46484375\" customWidth=\"1\"/>");
        sb.Append("<col min=\"3\" max=\"3\" width=\"66.46484375\" style=\"9\" customWidth=\"1\"/>");
        sb.Append("</cols>");

        sb.Append("<sheetData>");
        var hyperlinks = new StringBuilder();
        int hSeq = 0;
        int rowIdx = 1;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var combined = $"{item.AgencyName}：{item.TenderName}";
            sb.Append($"<row r=\"{rowIdx}\" spans=\"1:3\">");
            AppendInlineStr(sb, $"A{rowIdx}", 2,  item.AgencyName);
            AppendInlineStr(sb, $"B{rowIdx}", 10, item.TenderName);
            AppendInlineStr(sb, $"C{rowIdx}", 8,  combined);
            sb.Append("</row>");

            if (!string.IsNullOrWhiteSpace(item.DetailUrl))
            {
                hyperlinks.Append($"<hyperlink ref=\"B{rowIdx}\" r:id=\"rIdS3H{++hSeq}\"/>");
                hyperlinks.Append($"<hyperlink ref=\"C{rowIdx}\" r:id=\"rIdS3H{++hSeq}\"/>");
            }

            rowIdx++;
        }
        sb.Append("</sheetData>");

        sb.Append("<phoneticPr fontId=\"2\" type=\"noConversion\"/>");
        if (hyperlinks.Length > 0)
            sb.Append("<hyperlinks>").Append(hyperlinks).Append("</hyperlinks>");
        sb.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        sb.Append($"<drawing r:id=\"{S3DrawingRelId}\"/>");
        sb.Append("</worksheet>");

        return sb.ToString();
    }

    private static string BuildSheet3Rels(IReadOnlyList<TenderItem> items)
    {
        var sb = new StringBuilder(2 * 1024 + items.Count * 400);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        sb.Append($"<Relationship Id=\"{S3DrawingRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing3.xml\"/>");

        int hSeq = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.DetailUrl)) continue;
            var safe = EscapeXmlAttr(item.DetailUrl);
            sb.Append($"<Relationship Id=\"rIdS3H{++hSeq}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{safe}\" TargetMode=\"External\"/>");
            sb.Append($"<Relationship Id=\"rIdS3H{++hSeq}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{safe}\" TargetMode=\"External\"/>");
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // =================================================================
    // Helpers
    // =================================================================
    private static void AppendInlineStr(StringBuilder sb, string cellRef, int styleId, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            sb.Append($"<c r=\"{cellRef}\" s=\"{styleId}\"/>");
            return;
        }
        sb.Append($"<c r=\"{cellRef}\" s=\"{styleId}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{EscapeXmlText(SanitizeForExcel(value))}</t></is></c>");
    }

    private static string EscapeXmlText(string s)
    {
        var sb = new StringBuilder(s.Length + 16);
        foreach (var ch in s)
        {
            if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static string EscapeXmlAttr(string s)
    {
        var sb = new StringBuilder(s.Length + 16);
        foreach (var ch in s)
        {
            if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
            switch (ch)
            {
                case '&':  sb.Append("&amp;"); break;
                case '<':  sb.Append("&lt;"); break;
                case '>':  sb.Append("&gt;"); break;
                case '"':  sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default:   sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static string SanitizeForExcel(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var first = value[0];
        return (first == '=' || first == '+' || first == '-' || first == '@'
                || first == '\t' || first == '\r')
            ? "'" + value
            : value;
    }
}
