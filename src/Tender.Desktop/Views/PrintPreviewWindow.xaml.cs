using System.Windows;
using System.Windows.Documents;

namespace Tender.Desktop.Views;

public partial class PrintPreviewWindow : Window
{
    private readonly FlowDocument _document;
    private readonly string _title;

    public PrintPreviewWindow(FlowDocument document, string title)
    {
        InitializeComponent();
        _document = document;
        _title = title;

        // 預覽用 A4 縱向（96 dpi）；實際列印時會以 PrintDialog 選定的 PrintableArea 覆寫。
        document.PageHeight = 1122.24;  // ~11.69 inches
        document.PageWidth = 793.92;    // ~8.27 inches
        document.PagePadding = new Thickness(40);
        document.ColumnGap = 0;
        document.ColumnWidth = double.PositiveInfinity;

        Reader.Document = document;
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;

        // FlowDocument 同時被 Reader 持有，需暫時卸下才能更換頁面尺寸並送印
        Reader.Document = null;
        try
        {
            _document.PageHeight = dlg.PrintableAreaHeight;
            _document.PageWidth = dlg.PrintableAreaWidth;
            IDocumentPaginatorSource src = _document;
            dlg.PrintDocument(src.DocumentPaginator, _title);
        }
        finally
        {
            // 還原預覽尺寸並重新掛回 Reader（讓使用者能再次預覽 / 列印）
            _document.PageHeight = 1122.24;
            _document.PageWidth = 793.92;
            Reader.Document = _document;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
