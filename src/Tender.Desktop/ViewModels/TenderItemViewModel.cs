using CommunityToolkit.Mvvm.ComponentModel;
using Tender.Core.Models;

namespace Tender.Desktop.ViewModels;

/// <summary>
/// 包裝 TenderItem 以暴露 UI 端可變狀態（收藏、備註等）。
/// 收藏狀態變動透過事件通知父層 (DailyQueryViewModel) 持久化到 user-marks.json。
/// </summary>
public partial class TenderItemViewModel : ObservableObject
{
    public TenderItem Item { get; }

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _note = string.Empty;

    public string SourcePk => Item.SourcePk;
    public string AgencyName => Item.AgencyName;
    public string TenderName => Item.TenderName;
    public string TenderMethod => Item.TenderMethod;
    public string? ProcurementType => Item.ProcurementType;
    public string AnnouncementDate => Item.AnnouncementDate;
    public string? BidDeadline => Item.BidDeadline;
    public long? BudgetAmount => Item.BudgetAmount;
    public string DetailUrl => Item.DetailUrl;
    public IReadOnlyList<string> MatchedKeywords => Item.MatchedKeywords;

    public event Action<TenderItemViewModel>? FavoriteToggled;

    public TenderItemViewModel(TenderItem item, bool isFavorite, string note = "")
    {
        Item = item;
        _isFavorite = isFavorite;
        _note = note;
    }

    partial void OnIsFavoriteChanged(bool value) => FavoriteToggled?.Invoke(this);
}
