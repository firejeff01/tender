using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using Tender.Desktop.ViewModels;

namespace Tender.Desktop.Services;

/// <summary>
/// 把 TenderItemViewModel 集合產出 FlowDocument 並透過 PrintDialog 列印。
/// </summary>
public static class PrintHelper
{
    public static void Print(IReadOnlyList<TenderItemViewModel> items, string title)
    {
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;

        var doc = BuildFlowDocument(items, title);
        doc.PageHeight = dlg.PrintableAreaHeight;
        doc.PageWidth = dlg.PrintableAreaWidth;
        doc.PagePadding = new Thickness(40);
        doc.ColumnGap = 0;
        doc.ColumnWidth = double.PositiveInfinity;

        IDocumentPaginatorSource src = doc;
        dlg.PrintDocument(src.DocumentPaginator, title);
    }

    private static FlowDocument BuildFlowDocument(IReadOnlyList<TenderItemViewModel> items, string title)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Microsoft JhengHei UI"),
            FontSize = 11,
        };

        // 標題
        var head = new Paragraph(new Run(title))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        doc.Blocks.Add(head);

        var subtitle = new Paragraph(new Run($"列印時間：{DateTime.Now:yyyy-MM-dd HH:mm}　共 {items.Count:N0} 筆"))
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 12),
        };
        doc.Blocks.Add(subtitle);

        // 表格
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0.5),
        };
        table.Columns.Add(new TableColumn { Width = new GridLength(150) });   // 機關
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // 標案名稱
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });    // 招標方式
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });    // 公告日期
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });    // 截止
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });    // 預算

        // 表頭
        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow { Background = Brushes.LightGray };
        foreach (var h in new[] { "機關名稱", "標案名稱", "招標方式", "公告日期", "截止投標", "預算金額" })
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h)) { FontWeight = FontWeights.Bold })
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(4, 2, 4, 2),
            });
        }
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        // 資料列
        var dataGroup = new TableRowGroup();
        bool alt = false;
        foreach (var it in items)
        {
            var row = new TableRow();
            if (alt) row.Background = Brushes.WhiteSmoke;
            alt = !alt;
            row.Cells.Add(MakeCell(it.AgencyName));
            row.Cells.Add(MakeCell(it.TenderName));
            row.Cells.Add(MakeCell(it.TenderMethod));
            row.Cells.Add(MakeCell(it.AnnouncementDate));
            row.Cells.Add(MakeCell(it.BidDeadline ?? "—"));
            row.Cells.Add(MakeCell(it.BudgetAmount?.ToString("N0") ?? "—", TextAlignment.Right));
            dataGroup.Rows.Add(row);
        }
        table.RowGroups.Add(dataGroup);

        doc.Blocks.Add(table);
        return doc;
    }

    private static TableCell MakeCell(string text, TextAlignment align = TextAlignment.Left)
    {
        var p = new Paragraph(new Run(text)) { TextAlignment = align };
        return new TableCell(p)
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(4, 2, 4, 2),
        };
    }
}
