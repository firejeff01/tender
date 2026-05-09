using Tender.Desktop.Services;
using Tender.Desktop.ViewModels;

namespace Tender.Desktop.Views;

public partial class DailyQueryView : UserControl
{
    public DailyQueryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DailyQueryViewModel oldVm)
            oldVm.PrintRequested -= OnPrintRequested;
        if (e.NewValue is DailyQueryViewModel newVm)
            newVm.PrintRequested += OnPrintRequested;
    }

    private void OnPrintRequested(System.Collections.Generic.IReadOnlyList<TenderItemViewModel> items)
    {
        if (DataContext is DailyQueryViewModel vm)
        {
            var title = vm.IsRangeMode
                ? $"標案查詢　{vm.Date:yyyy-MM-dd} ~ {vm.DateTo:yyyy-MM-dd}"
                : $"標案查詢　{vm.Date:yyyy-MM-dd}";
            PrintHelper.Print(items, title);
        }
    }

    private void DigitsOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // 只接受 0-9
        foreach (var ch in e.Text)
        {
            if (ch < '0' || ch > '9') { e.Handled = true; return; }
        }
    }

    private void DigitsOnly_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        if (e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) is not string text)
        {
            e.CancelCommand();
            return;
        }
        foreach (var ch in text)
        {
            if (ch < '0' || ch > '9') { e.CancelCommand(); return; }
        }
    }
}
