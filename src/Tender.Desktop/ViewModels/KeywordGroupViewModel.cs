using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tender.Desktop.ViewModels;

/// <summary>
/// 關鍵字群組（如「地區」、「資訊系統」），底下含若干個 KeywordButtonViewModel。
/// </summary>
public partial class KeywordGroupViewModel : ObservableObject
{
    public string AccentColor { get; }

    public string Name { get; }

    public ObservableCollection<KeywordButtonViewModel> Buttons { get; }

    [ObservableProperty]
    private int _activeCount;

    public bool HasActive => ActiveCount > 0;

    /// <summary>當任一按鈕 IsActive 變更時呼叫，由父層 (DailyQueryViewModel) 訂閱以觸發 ApplyFilter。</summary>
    public event Action? AnyButtonToggled;

    public KeywordGroupViewModel(string name, string accentColor, IEnumerable<string> keywords)
    {
        Name = name;
        AccentColor = accentColor;
        Buttons = new ObservableCollection<KeywordButtonViewModel>();
        foreach (var k in keywords)
        {
            var btn = new KeywordButtonViewModel(k);
            btn.PropertyChanged += OnButtonPropertyChanged;
            Buttons.Add(btn);
        }
    }

    private void OnButtonPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(KeywordButtonViewModel.IsActive)) return;
        ActiveCount = Buttons.Count(b => b.IsActive);
        OnPropertyChanged(nameof(HasActive));
        AnyButtonToggled?.Invoke();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var b in Buttons) b.IsActive = true;
    }

    [RelayCommand]
    private void ClearGroup()
    {
        foreach (var b in Buttons) b.IsActive = false;
    }
}
