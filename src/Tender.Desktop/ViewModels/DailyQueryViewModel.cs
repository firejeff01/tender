using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Clock;
using Tender.Core.Models;
using Tender.Core.Search;
using Tender.Desktop.Services;
using Tender.Storage.Repositories;

namespace Tender.Desktop.ViewModels;

public partial class DailyQueryViewModel : ObservableObject
{
    private readonly ITenderRepository _tenderRepo;
    private readonly IBrowserLauncher _browser;
    private readonly ISearchService _searchService;
    private readonly IKeywordsRepository _keywordsRepo;
    private readonly IClock _clock;
    private readonly IExcelExporter _excelExporter;
    private readonly ISaveFileDialogService _saveDialog;
    private readonly IUserMarksRepository _userMarksRepo;

    /// <summary>群組顯示色（暖色系，依出現順序循環）。</summary>
    private static readonly string[] GroupAccentColors =
    {
        "#8B6F47", "#9C5A8C", "#A0524D", "#7A8B5C", "#C4823C",
        "#5B7C8C", "#8B5A3C", "#6B8E6B", "#A5664E",
    };

    /// <summary>記憶體中的 user-marks 表（sourcePk → UserMark），由 LoadAsync 填入。</summary>
    private Dictionary<string, UserMark> _userMarks = new();

    [ObservableProperty]
    private DateOnly _date;

    [ObservableProperty]
    private IReadOnlyList<TenderItemViewModel> _allItems = Array.Empty<TenderItemViewModel>();

    [ObservableProperty]
    private IReadOnlyList<TenderItemViewModel> _filteredItems = Array.Empty<TenderItemViewModel>();

    [ObservableProperty]
    private TenderItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _keywordQuery;

    [ObservableProperty]
    private SortKey _sortKey = SortKey.None;

    [ObservableProperty]
    private SortDirection _sortDirection = SortDirection.Descending;

    [ObservableProperty]
    private bool _showActiveOnly;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private string? _selectedTenderMethod;

    [ObservableProperty]
    private string? _selectedProcurementType;

    [ObservableProperty]
    private long? _budgetMin;

    [ObservableProperty]
    private long? _budgetMax;

    [ObservableProperty]
    private DateTime? _bidDeadlineFrom;

    [ObservableProperty]
    private DateTime? _bidDeadlineTo;

    public ObservableCollection<KeywordGroupViewModel> KeywordGroups { get; } = new();
    public ObservableCollection<string> AvailableTenderMethods { get; } = new();
    public ObservableCollection<string> AvailableProcurementTypes { get; } = new();

    public DailyQueryViewModel(
        ITenderRepository tenderRepo,
        IBrowserLauncher browser,
        ISearchService searchService,
        IKeywordsRepository keywordsRepo,
        IClock clock,
        IExcelExporter excelExporter,
        ISaveFileDialogService saveDialog,
        IUserMarksRepository userMarksRepo)
    {
        _tenderRepo = tenderRepo;
        _browser = browser;
        _searchService = searchService;
        _keywordsRepo = keywordsRepo;
        _clock = clock;
        _excelExporter = excelExporter;
        _saveDialog = saveDialog;
        _userMarksRepo = userMarksRepo;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // 讀取 user-marks
            var marks = await _userMarksRepo.LoadAsync(ct);
            _userMarks = marks.Marks.ToDictionary(m => m.SourcePk, m => m);

            // 讀取當日標案
            var snapshot = await _tenderRepo.LoadAsync(Date, ct);
            var rawItems = snapshot?.Items ?? Array.Empty<TenderItem>();

            // 包裝成 ViewModel
            var wrapped = rawItems.Select(item =>
            {
                var hasMark = _userMarks.TryGetValue(item.SourcePk, out var mark);
                var vm = new TenderItemViewModel(
                    item,
                    isFavorite: hasMark && mark!.IsFavorite,
                    note: hasMark ? mark!.Note : string.Empty);
                vm.FavoriteToggled += OnFavoriteToggledAsync;
                return vm;
            }).ToList().AsReadOnly();
            AllItems = wrapped;

            // 第一次載入：建立關鍵字按鈕群組
            if (KeywordGroups.Count == 0)
            {
                var keywordSet = await _keywordsRepo.LoadAsync(ct);
                int colorIdx = 0;
                foreach (var group in keywordSet.Groups)
                {
                    var keywords = group.Items
                        .Where(k => k.Enabled)
                        .Select(k => k.Keyword);
                    var color = GroupAccentColors[colorIdx % GroupAccentColors.Length];
                    var groupVm = new KeywordGroupViewModel(group.Name, color, keywords);
                    groupVm.AnyButtonToggled += ApplyFilter;
                    KeywordGroups.Add(groupVm);
                    colorIdx++;
                }
            }

            RebuildDropdowns();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            AllItems = Array.Empty<TenderItemViewModel>();
            FilteredItems = Array.Empty<TenderItemViewModel>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildDropdowns()
    {
        AvailableTenderMethods.Clear();
        AvailableTenderMethods.Add("（不限）");
        foreach (var m in AllItems.Select(i => i.TenderMethod).Distinct().OrderBy(s => s))
            AvailableTenderMethods.Add(m);

        AvailableProcurementTypes.Clear();
        AvailableProcurementTypes.Add("（不限）");
        foreach (var p in AllItems.Select(i => i.ProcurementType).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s))
            AvailableProcurementTypes.Add(p!);

        if (string.IsNullOrEmpty(SelectedTenderMethod))
            SelectedTenderMethod = "（不限）";
        if (string.IsNullOrEmpty(SelectedProcurementType))
            SelectedProcurementType = "（不限）";
    }

    private async void OnFavoriteToggledAsync(TenderItemViewModel vm)
    {
        // 更新記憶體
        if (_userMarks.TryGetValue(vm.SourcePk, out var existing))
        {
            _userMarks[vm.SourcePk] = existing with { IsFavorite = vm.IsFavorite };
        }
        else
        {
            _userMarks[vm.SourcePk] = new UserMark
            {
                SourcePk = vm.SourcePk,
                IsFavorite = vm.IsFavorite,
            };
        }

        // 寫盤（保留 IsRead/IsExcluded/Note 欄位）
        try
        {
            var marks = new UserMarks
            {
                Marks = _userMarks.Values.Where(m => m.IsFavorite || m.IsRead || m.IsExcluded || !string.IsNullOrEmpty(m.Note))
                                          .ToList()
                                          .AsReadOnly(),
            };
            await _userMarksRepo.SaveAsync(marks);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"儲存收藏失敗：{ex.Message}";
        }

        // 收藏濾鏡開啟時，狀態變動可能影響可見列表
        if (ShowFavoritesOnly) ApplyFilter();
    }

    [RelayCommand]
    private void OpenDetail(TenderItemViewModel? item)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.DetailUrl))
            _browser.Open(item.DetailUrl);
    }

    [RelayCommand]
    private void ClearAllFilters()
    {
        foreach (var group in KeywordGroups)
            foreach (var btn in group.Buttons)
                btn.IsActive = false;
        KeywordQuery = null;
        ShowActiveOnly = false;
        ShowFavoritesOnly = false;
        SelectedTenderMethod = "（不限）";
        SelectedProcurementType = "（不限）";
        BudgetMin = null;
        BudgetMax = null;
        BidDeadlineFrom = null;
        BidDeadlineTo = null;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task GoPreviousDayAsync(CancellationToken ct)
    {
        Date = Date.AddDays(-1);
        await LoadAsync(ct);
    }

    [RelayCommand]
    private async Task GoNextDayAsync(CancellationToken ct)
    {
        Date = Date.AddDays(1);
        await LoadAsync(ct);
    }

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        if (FilteredItems.Count == 0)
        {
            ErrorMessage = "目前沒有可匯出的資料";
            return;
        }

        var suggested = $"標案_{Date:yyyyMMdd}.xlsx";
        var savePath = _saveDialog.ShowSaveAsXlsx(suggested);
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            IsLoading = true;
            // 解包 ViewModel → TenderItem 給 Exporter
            var rawItems = FilteredItems.Select(vm => vm.Item).ToList().AsReadOnly();
            await _excelExporter.ExportAsync(rawItems, savePath, ct);
            ErrorMessage = $"已匯出 {FilteredItems.Count} 筆 → {savePath}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"匯出失敗：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnKeywordQueryChanged(string? value) => ApplyFilter();
    partial void OnSortKeyChanged(SortKey value) => ApplyFilter();
    partial void OnSortDirectionChanged(SortDirection value) => ApplyFilter();
    partial void OnShowActiveOnlyChanged(bool value) => ApplyFilter();
    partial void OnShowFavoritesOnlyChanged(bool value) => ApplyFilter();
    partial void OnSelectedTenderMethodChanged(string? value) => ApplyFilter();
    partial void OnSelectedProcurementTypeChanged(string? value) => ApplyFilter();
    partial void OnBudgetMinChanged(long? value) => ApplyFilter();
    partial void OnBudgetMaxChanged(long? value) => ApplyFilter();
    partial void OnBidDeadlineFromChanged(DateTime? value) => ApplyFilter();
    partial void OnBidDeadlineToChanged(DateTime? value) => ApplyFilter();

    private static string? ToRocDate(DateTime? d)
        => d.HasValue ? $"{d.Value.Year - 1911}/{d.Value.Month:D2}/{d.Value.Day:D2}" : null;

    private void ApplyFilter()
    {
        if (AllItems.Count == 0)
        {
            FilteredItems = AllItems;
            return;
        }

        var activeKeywords = KeywordGroups
            .SelectMany(g => g.Buttons)
            .Where(b => b.IsActive)
            .Select(b => b.Keyword)
            .Distinct()
            .ToList()
            .AsReadOnly();

        var criteria = new SearchCriteria
        {
            KeywordQuery = KeywordQuery,
            ActiveKeywordButtons = activeKeywords,
            ShowActiveOnly = ShowActiveOnly,
            TenderMethod = SelectedTenderMethod == "（不限）" ? null : SelectedTenderMethod,
            ProcurementType = SelectedProcurementType == "（不限）" ? null : SelectedProcurementType,
            BudgetMin = BudgetMin,
            BudgetMax = BudgetMax,
            BidDeadlineFrom = ToRocDate(BidDeadlineFrom),
            BidDeadlineTo = ToRocDate(BidDeadlineTo),
        };

        var today = DateOnly.FromDateTime(_clock.Now.LocalDateTime);

        // SearchService 對 raw TenderItem 操作
        var rawAll = AllItems.Select(vm => vm.Item).ToList().AsReadOnly();
        var rawFiltered = _searchService.Search(rawAll, criteria, SortKey, SortDirection, today);

        // 把過濾結果 sourcePk 對回 ViewModel（保留 IsFavorite 等狀態）
        var vmByPk = AllItems.ToDictionary(v => v.SourcePk);
        var filtered = rawFiltered
            .Select(item => vmByPk[item.SourcePk])
            .Where(vm => !ShowFavoritesOnly || vm.IsFavorite)
            .ToList()
            .AsReadOnly();

        FilteredItems = filtered;
    }
}
