using ClosedXML.Excel;
using Tender.Core.Models;

namespace Tender.Desktop.Services;

public sealed class ClosedXmlExcelExporter : IExcelExporter
{
    private static readonly string[] Headers =
    {
        "標案名稱", "招標方式", "採購性質",
        "公告日期", "截止投標", "預算金額",
        "機關名稱", "檢視連結", "機關名稱：標案名稱",
        "命中關鍵字",
    };

    public Task ExportAsync(IReadOnlyList<TenderItem> items, string savePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("標案");

            // 表頭
            for (int c = 0; c < Headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = Headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            // 資料列
            int row = 2;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                // A: 標案名稱（hyperlink）
                var nameCell = ws.Cell(row, 1);
                nameCell.Value = SanitizeForExcel(item.TenderName);
                if (!string.IsNullOrWhiteSpace(item.DetailUrl))
                {
                    nameCell.SetHyperlink(new XLHyperlink(item.DetailUrl));
                    nameCell.Style.Font.FontColor = XLColor.Blue;
                    nameCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                }

                // B: 招標方式
                ws.Cell(row, 2).Value = SanitizeForExcel(item.TenderMethod);
                // C: 採購性質
                ws.Cell(row, 3).Value = SanitizeForExcel(item.ProcurementType);
                // D: 公告日期（text）
                ws.Cell(row, 4).Value = SanitizeForExcel(item.AnnouncementDate);
                ws.Cell(row, 4).Style.NumberFormat.Format = "@";
                // E: 截止投標（text）
                ws.Cell(row, 5).Value = SanitizeForExcel(item.BidDeadline);
                ws.Cell(row, 5).Style.NumberFormat.Format = "@";
                // F: 預算金額（number）
                if (item.BudgetAmount.HasValue)
                {
                    ws.Cell(row, 6).Value = item.BudgetAmount.Value;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                }
                // G: 機關名稱
                ws.Cell(row, 7).Value = SanitizeForExcel(item.AgencyName);

                // H: 檢視連結（hyperlink）
                if (!string.IsNullOrWhiteSpace(item.DetailUrl))
                {
                    var linkCell = ws.Cell(row, 8);
                    linkCell.Value = "檢視";
                    linkCell.SetHyperlink(new XLHyperlink(item.DetailUrl));
                    linkCell.Style.Font.FontColor = XLColor.Blue;
                    linkCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                }

                // I: 機關名稱：標案名稱（hyperlink）
                var combinedCell = ws.Cell(row, 9);
                combinedCell.Value = SanitizeForExcel($"{item.AgencyName}：{item.TenderName}");
                if (!string.IsNullOrWhiteSpace(item.DetailUrl))
                {
                    combinedCell.SetHyperlink(new XLHyperlink(item.DetailUrl));
                    combinedCell.Style.Font.FontColor = XLColor.Blue;
                    combinedCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                }

                // J: 命中關鍵字
                ws.Cell(row, 10).Value = SanitizeForExcel(string.Join("、", item.MatchedKeywords));

                row++;
            }

            // 凍結首列、自動寬度
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents(minWidth: 8, maxWidth: 60);

            wb.SaveAs(savePath);
        }, ct);
    }

    // 防 Excel/CSV formula injection：上游字串若以 = + - @ \t \r 開頭，
    // 使用者在 Excel 中按 F2 + Enter 可能會被重新解讀為公式（例如
    // =HYPERLINK("https://evil","click")、=cmd|'/c calc'!A0）。前置一個
    // 單引號讓 Excel 視為純文字，符合 OWASP 建議。
    private static string SanitizeForExcel(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var first = value[0];
        return (first == '=' || first == '+' || first == '-' || first == '@'
                || first == '\t' || first == '\r')
            ? "'" + value
            : value;
    }
}
