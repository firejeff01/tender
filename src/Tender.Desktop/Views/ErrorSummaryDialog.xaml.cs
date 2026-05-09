using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Tender.Desktop.Views;

public partial class ErrorSummaryDialog : Window
{
    private string? _errorsLogPath;

    public ErrorSummaryDialog()
    {
        InitializeComponent();
    }

    public void Initialize(DateOnly date, string errorMessage, string errorsLogPath)
    {
        _errorsLogPath = errorsLogPath;
        TitleText.Text = $"⚠ {date:yyyy-MM-dd} 爬蟲錯誤";
        ErrorMessageText.Text = string.IsNullOrWhiteSpace(errorMessage) ? "（無摘要訊息）" : errorMessage;

        try
        {
            if (File.Exists(errorsLogPath))
                LogContent.Text = File.ReadAllText(errorsLogPath);
            else
                LogContent.Text = "（找不到 errors.log）";
        }
        catch (Exception ex)
        {
            LogContent.Text = $"讀取 errors.log 失敗：{ex.Message}";
        }
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_errorsLogPath) || !File.Exists(_errorsLogPath))
        {
            MessageBox.Show("找不到 errors.log", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(_errorsLogPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"開啟失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
