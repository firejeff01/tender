using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Tender.Core.Models;

namespace Tender.Desktop.Services;

/// <summary>
/// 共用的 .xlsm 範本匯出器：依 <see cref="XlsmTemplateProfile"/> 從 Templates 複製對應 .xlsm 範本，
/// 再重寫 sheet1「全部資料」與 sheet2「篩選」的 worksheet xml + rels。
///
/// 不動 vbaProject.bin、drawings、styles.xml、sharedStrings.xml — VBA 巨集 / 按鈕配置 / 樣式皆維持原樣，
/// 所以每個 profile 必須使用該範本實際存在的 cellXfs 索引與 sheet rel id，
/// 否則開檔會跳「已修復的記錄」或樣式錯亂。
/// </summary>
internal sealed class ProfiledTemplateXlsmExporter
{
    private const string Sheet1Path = "xl/worksheets/sheet1.xml";
    private const string Sheet1RelsPath = "xl/worksheets/_rels/sheet1.xml.rels";
    private const string Sheet2Path = "xl/worksheets/sheet2.xml";
    private const string Sheet2RelsPath = "xl/worksheets/_rels/sheet2.xml.rels";

    private readonly XlsmTemplateProfile _profile;

    public ProfiledTemplateXlsmExporter(XlsmTemplateProfile profile) => _profile = profile;

    public Task ExportAsync(
        IReadOnlyList<TenderItem> allItems,
        IReadOnlyList<TenderItem> filteredItems,
        string savePath,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var templatePath = ResolveTemplatePath(_profile.TemplateRelativePath);
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"找不到 xlsm 範本：{templatePath}");

            File.Copy(templatePath, savePath, overwrite: true);

            using var zip = ZipFile.Open(savePath, ZipArchiveMode.Update);
            ReplaceEntry(zip, Sheet1Path,     BuildSheet1Xml(allItems, ct));
            ReplaceEntry(zip, Sheet1RelsPath, BuildSheet1Rels(allItems));
            ReplaceEntry(zip, Sheet2Path,     BuildSheet2Xml(filteredItems, ct));
            ReplaceEntry(zip, Sheet2RelsPath, BuildSheet2Rels(filteredItems));
        }, ct);
    }

    private static string ResolveTemplatePath(string relative)
    {
        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                      ?? AppContext.BaseDirectory;
        return Path.Combine(baseDir, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ReplaceEntry(ZipArchive zip, string entryName, string content)
    {
        zip.GetEntry(entryName)?.Delete();
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var sw = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.Write(content);
    }

    // =================================================================
    // sheet1「全部資料」：allItems，欄位 B-J
    // =================================================================
    private string BuildSheet1Xml(IReadOnlyList<TenderItem> items, CancellationToken ct)
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
        sb.Append("<pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        sb.Append("<selection pane=\"bottomLeft\" activeCell=\"B3\" sqref=\"B3\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append(_profile.Sheet1SheetFormatPr);
        sb.Append(_profile.Sheet1Cols);

        sb.Append("<sheetData>");
        sb.Append("<row r=\"1\" spans=\"2:10\" ht=\"133.9\" customHeight=\"1\"/>");
        sb.Append("<row r=\"2\" spans=\"2:10\" ht=\"42\" customHeight=\"1\">");
        var headS = _profile.Sheet1HeaderStyle;
        var headJ = _profile.Sheet1HeaderJStyle;
        sb.Append($"<c r=\"B2\" s=\"{headS}\" t=\"s\"><v>2</v></c>");
        sb.Append($"<c r=\"C2\" s=\"{headS}\"/>");
        sb.Append($"<c r=\"D2\" s=\"{headS}\" t=\"s\"><v>1</v></c>");
        sb.Append($"<c r=\"E2\" s=\"{headS}\" t=\"s\"><v>3</v></c>");
        sb.Append($"<c r=\"F2\" s=\"{headS}\" t=\"s\"><v>4</v></c>");
        sb.Append($"<c r=\"G2\" s=\"{headS}\" t=\"s\"><v>5</v></c>");
        sb.Append($"<c r=\"H2\" s=\"{_profile.Sheet1HeaderHStyle}\" t=\"s\"><v>6</v></c>");
        sb.Append($"<c r=\"I2\" s=\"{headS}\" t=\"s\"><v>0</v></c>");
        sb.Append($"<c r=\"J2\" s=\"{headJ}\"/>");
        sb.Append("</row>");

        var sB = _profile.Sheet1DataBStyle;
        var sC = _profile.Sheet1DataCStyle;
        var sD = _profile.Sheet1DataDStyle;
        var sE = _profile.Sheet1DataEStyle;
        var sF = _profile.Sheet1DataFStyle;
        var sG = _profile.Sheet1DataGStyle;
        var sH = _profile.Sheet1DataHStyle;
        var sI = _profile.Sheet1DataIStyle;
        var sJ = _profile.Sheet1DataJStyle;

        var hyperlinks = new StringBuilder();
        int hSeq = 0;
        int rowIdx = 3;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            sb.Append($"<row r=\"{rowIdx}\" spans=\"2:10\" ht=\"16.5\" customHeight=\"1\">");
            AppendInlineStr(sb, $"B{rowIdx}", sB,  item.TenderName);
            if (string.IsNullOrEmpty(item.TenderNo))
                sb.Append($"<c r=\"C{rowIdx}\" s=\"{sC}\"/>");
            else
                AppendInlineStr(sb, $"C{rowIdx}", sC, item.TenderNo);
            AppendInlineStr(sb, $"D{rowIdx}", sD,  item.TenderMethod);
            AppendInlineStr(sb, $"E{rowIdx}", sE,  item.ProcurementType);
            AppendInlineStr(sb, $"F{rowIdx}", sF,  item.AnnouncementDate);
            AppendInlineStr(sb, $"G{rowIdx}", sG,  item.BidDeadline);
            if (item.BudgetAmount.HasValue)
                sb.Append($"<c r=\"H{rowIdx}\" s=\"{sH}\"><v>{item.BudgetAmount.Value}</v></c>");
            else
                sb.Append($"<c r=\"H{rowIdx}\" s=\"{sH}\"/>");
            AppendInlineStr(sb, $"I{rowIdx}", sI,  item.AgencyName);
            AppendInlineStr(sb, $"J{rowIdx}", sJ, "檢視");
            sb.Append("</row>");

            if (!string.IsNullOrWhiteSpace(item.DetailUrl))
            {
                hyperlinks.Append($"<hyperlink ref=\"B{rowIdx}\" r:id=\"rIdS1H{++hSeq}\"/>");
                hyperlinks.Append($"<hyperlink ref=\"J{rowIdx}\" r:id=\"rIdS1H{++hSeq}\"/>");
            }

            rowIdx++;
        }
        sb.Append("</sheetData>");

        // autoFilter：巨集（"機關名稱" / 機關代理篩選）依賴此處的範圍 + workbook 內 _FilterDatabase
        if (items.Count > 0)
            sb.Append($"<autoFilter ref=\"A1:I{lastRow}\"/>");

        // 順序依 OOXML schema：phoneticPr → hyperlinks → pageMargins → pageSetup → headerFooter → drawing
        sb.Append("<phoneticPr fontId=\"2\" type=\"noConversion\"/>");
        if (hyperlinks.Length > 0)
            sb.Append("<hyperlinks>").Append(hyperlinks).Append("</hyperlinks>");
        sb.Append("<pageMargins left=\"0.75\" right=\"0.75\" top=\"1\" bottom=\"1\" header=\"0.5\" footer=\"0.5\"/>");
        sb.Append($"<pageSetup paperSize=\"9\" orientation=\"portrait\" r:id=\"{_profile.Sheet1PrinterRelId}\"/>");
        sb.Append("<headerFooter alignWithMargins=\"0\"/>");
        sb.Append($"<drawing r:id=\"{_profile.Sheet1DrawingRelId}\"/>");
        sb.Append("</worksheet>");

        return sb.ToString();
    }

    private string BuildSheet1Rels(IReadOnlyList<TenderItem> items)
    {
        var sb = new StringBuilder(4 * 1024 + items.Count * 220);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        sb.Append($"<Relationship Id=\"{_profile.Sheet1PrinterRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/printerSettings\" Target=\"../printerSettings/printerSettings1.bin\"/>");
        sb.Append($"<Relationship Id=\"{_profile.Sheet1DrawingRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>");

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
    // sheet2「篩選」：filteredItems，B 欄一格放「機關名稱：標案名稱」+ link
    // =================================================================
    private string BuildSheet2Xml(IReadOnlyList<TenderItem> items, CancellationToken ct)
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
        sb.Append($"<col min=\"2\" max=\"2\" width=\"94.265625\" style=\"{_profile.Sheet2ColumnStyle}\" bestFit=\"1\" customWidth=\"1\"/>");
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
            AppendInlineStr(sb, $"B{rowIdx}", _profile.Sheet2DataBStyle, combined);
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
        sb.Append($"<pageSetup paperSize=\"9\" orientation=\"portrait\" r:id=\"{_profile.Sheet2PrinterRelId}\"/>");
        sb.Append("<headerFooter alignWithMargins=\"0\"/>");
        sb.Append($"<drawing r:id=\"{_profile.Sheet2DrawingRelId}\"/>");
        sb.Append("</worksheet>");

        return sb.ToString();
    }

    private string BuildSheet2Rels(IReadOnlyList<TenderItem> items)
    {
        var sb = new StringBuilder(2 * 1024 + items.Count * 200);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        sb.Append($"<Relationship Id=\"{_profile.Sheet2PrinterRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/printerSettings\" Target=\"../printerSettings/printerSettings2.bin\"/>");
        sb.Append($"<Relationship Id=\"{_profile.Sheet2DrawingRelId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing2.xml\"/>");

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
    // XML helpers（與 TemplateXlsmExporter 版本相同；inlineStr 不用動 sharedStrings）
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
