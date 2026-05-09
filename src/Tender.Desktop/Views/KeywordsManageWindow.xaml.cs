using System.Windows;
using Tender.Desktop.ViewModels;

namespace Tender.Desktop.Views;

public partial class KeywordsManageWindow : Window
{
    private readonly KeywordsManageViewModel _vm;

    public KeywordsManageWindow(KeywordsManageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "還有未儲存的變更，確定要關閉嗎？",
                "未儲存變更",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }
        Close();
    }
}
