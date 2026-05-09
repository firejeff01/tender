using CommunityToolkit.Mvvm.ComponentModel;

namespace Tender.Desktop.ViewModels;

/// <summary>單一關鍵字按鈕（如「高雄」、「ESG」）。</summary>
public partial class KeywordButtonViewModel : ObservableObject
{
    public string Keyword { get; }

    [ObservableProperty]
    private bool _isActive;

    public KeywordButtonViewModel(string keyword)
    {
        Keyword = keyword;
    }
}
