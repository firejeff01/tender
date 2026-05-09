using System.Windows;
using Tender.Desktop.Views;

namespace Tender.Desktop.Services;

public sealed class WpfErrorSummaryDialog : IErrorSummaryDialog
{
    public Task ShowAsync(DateOnly date, string errorMessage, string errorsLogPath)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dlg = new ErrorSummaryDialog
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.Initialize(date, errorMessage, errorsLogPath);
            dlg.ShowDialog();
        }).Task;
    }
}
