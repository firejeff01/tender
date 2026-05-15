using System.Windows;
using System.Windows.Controls;
using Tender.Desktop.Helpers;
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
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ListBoxReorderHelper.Attach(GroupsListBox, _vm.MoveGroup);
        ListBoxReorderHelper.Attach(KeywordsListBox, _vm.MoveKeyword);
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Popup 色盤的單一色塊被點到。Button.Tag 是該色 hex string；
    /// 從 button 沿 logical tree 往上找 Popup → PlacementTarget (ToggleButton) → DataContext = EditableGroup。
    /// </summary>
    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;

        DependencyObject? d = btn;
        while (d != null && d is not System.Windows.Controls.Primitives.Popup)
            d = LogicalTreeHelper.GetParent(d);

        if (d is System.Windows.Controls.Primitives.Popup popup &&
            popup.PlacementTarget is FrameworkElement target &&
            target.DataContext is EditableGroup group)
        {
            group.Color = hex;
            popup.IsOpen = false;
        }
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
