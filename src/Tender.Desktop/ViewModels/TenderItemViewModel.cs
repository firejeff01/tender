using CommunityToolkit.Mvvm.ComponentModel;
using Tender.Core.Models;

namespace Tender.Desktop.ViewModels;

/// <summary>
/// 包裝 TenderItem 以暴露 UI 端可變狀態（收藏 / 已讀 / 排除 / 備註）。
/// 任一欄位變動透過事件通知父層 (DailyQueryViewModel) 持久化到 user-marks.json。
/// </summary>
public partial class TenderItemViewModel : ObservableObject
{
    public TenderItem Item { get; }

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isRead;

    [ObservableProperty]
    private bool _isExcluded;

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

    public event Action<TenderItemViewModel, string>? MarkChanged;

    public TenderItemViewModel(TenderItem item, UserMark? mark = null)
    {
        Item = item;
        _isFavorite = mark?.IsFavorite ?? false;
        _isRead = mark?.IsRead ?? false;
        _isExcluded = mark?.IsExcluded ?? false;
        _note = mark?.Note ?? string.Empty;
    }

    partial void OnIsFavoriteChanged(bool value) => MarkChanged?.Invoke(this, nameof(IsFavorite));
    partial void OnIsReadChanged(bool value) => MarkChanged?.Invoke(this, nameof(IsRead));
    partial void OnIsExcludedChanged(bool value) => MarkChanged?.Invoke(this, nameof(IsExcluded));
    partial void OnNoteChanged(string value) => MarkChanged?.Invoke(this, nameof(Note));
}
