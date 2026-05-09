using System.Windows;
using Tender.Desktop.ViewModels;

namespace Tender.Desktop.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>選單按鈕：點擊後關閉設定 popup。</summary>
    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsMenuButton.IsChecked = false;
    }
}
