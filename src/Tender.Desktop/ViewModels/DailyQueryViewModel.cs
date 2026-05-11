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
    private readonly IXlsmExporter _xlsmExporter;
    private readonly ISaveFileDialogService _saveDialog;
    private readonly IUserMarksRepository _userMarksRepo;
    private readonly ISavedSearchesRepository _savedSearchesRepo;

    /// <summary>群組顯示色（暖色系，依出現順序循環）。</summary>
    private static readonly string[] GroupAccentColors =
    {
        "#8B6F47", "#9C5A8C", "#A0524D", "#7A8B5C", "#C4823C",
        "#5B7C8C", "#8B5A3C", "#6B8E6B", "#A5664E",
    };

    /// <summary>記憶體中的 user-marks 表（sourcePk → UserMark），由 LoadAsync 填入。</summary>
    private Dictionary<string, UserMark> _userMarks = new();

    /// <summary>批次操作期間 true，OnMarkChangedAsync 會跳過 disk I/O 與 ApplyFilter，由批次結束後統一觸發。</summary>
    private bool _suppressMarkSideEffects;

    /// <summary>true 時 Date/DateTo 改動不會 auto-reload，命令路徑用以避免雙重 LoadAsync。</summary>
    private bool _suppressDateAutoReload;

    [ObservableProperty]
    private DateOnly _date;

    /// <summary>跨日查詢的迄止日（含）；null 代表只查 Date 一天。</summary>
    [ObservableProperty]
    private DateOnly? _dateTo;

    public bool IsRangeMode => DateTo.HasValue && DateTo.Value > Date;

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

    /// <summary>搜尋僅比對標案名稱（不含機關名稱）。</summary>
    [ObservableProperty]
    private bool _keywordTitleOnly;

    /// <summary>反向搜尋：標題不含關鍵字才保留。僅在 KeywordTitleOnly = true 時可勾選。</summary>
    [ObservableProperty]
    private bool _keywordExclude;

    [ObservableProperty]
    private SortKey _sortKey = SortKey.None;

    [ObservableProperty]
    private SortDirection _sortDirection = SortDirection.Descending;

    [ObservableProperty]
    private bool _showActiveOnly;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    /// <summary>顯示已排除的標案（預設不顯示）。</summary>
    [ObservableProperty]
    private bool _showExcluded;

    [ObservableProperty]
    private bool _showUnreadOnly;

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
    public ObservableCollection<SavedSearch> SavedSearches { get; } = new();

    public DailyQueryViewModel(
        ITenderRepository tenderRepo,
        IBrowserLauncher browser,
        ISearchService searchService,
        IKeywordsRepository keywordsRepo,
        IClock clock,
        IExcelExporter excelExporter,
        IXlsmExporter xlsmExporter,
        ISaveFileDialogService saveDialog,
        IUserMarksRepository userMarksRepo,
        ISavedSearchesRepository savedSearchesRepo)
    {
        _tenderRepo = tenderRepo;
        _browser = browser;
        _searchService = searchService;
        _keywordsRepo = keywordsRepo;
        _clock = clock;
        _excelExporter = excelExporter;
        _xlsmExporter = xlsmExporter;
        _saveDialog = saveDialog;
        _userMarksRepo = userMarksRepo;
        _savedSearchesRepo = savedSearchesRepo;
    }

    [RelayCommand]
    private async Task LoadSavedSearchesAsync(CancellationToken ct)
    {
        try
        {
            var set = await _savedSearchesRepo.LoadAsync(ct);
            SavedSearches.Clear();
            foreach (var s in set.Searches) SavedSearches.Add(s);
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async Task SaveCurrentSearchAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var search = new SavedSearch
        {
            Name = name,
            KeywordQuery = KeywordQuery,
            KeywordTitleOnly = KeywordTitleOnly,
            KeywordExclude = KeywordExclude,
            ActiveKeywords = KeywordGroups.SelectMany(g => g.Buttons).Where(b => b.IsActive).Select(b => b.Keyword).ToList().AsReadOnly(),
            TenderMethod = SelectedTenderMethod == "（不限）" ? null : SelectedTenderMethod,
            ProcurementType = SelectedProcurementType == "（不限）" ? null : SelectedProcurementType,
            BudgetMin = BudgetMin,
            BudgetMax = BudgetMax,
            ShowActiveOnly = ShowActiveOnly,
            ShowFavoritesOnly = ShowFavoritesOnly,
        };
        // 同名覆蓋
        var existing = SavedSearches.FirstOrDefault(s => s.Name == name);
        if (existing != null) SavedSearches.Remove(existing);
        SavedSearches.Add(search);
        await _savedSearchesRepo.SaveAsync(new SavedSearchSet { Searches = SavedSearches.ToList().AsReadOnly() });
    }

    [RelayCommand]
    private void ApplySavedSearch(SavedSearch? search)
    {
        if (search == null) return;
        KeywordQuery = search.KeywordQuery;
        KeywordTitleOnly = search.KeywordTitleOnly;
        KeywordExclude = search.KeywordExclude;
        var activeSet = new HashSet<string>(search.ActiveKeywords);
        foreach (var btn in KeywordGroups.SelectMany(g => g.Buttons))
            btn.IsActive = activeSet.Contains(btn.Keyword);
        SelectedTenderMethod = search.TenderMethod ?? "（不限）";
        SelectedProcurementType = search.ProcurementType ?? "（不限）";
        BudgetMin = search.BudgetMin;
        BudgetMax = search.BudgetMax;
        ShowActiveOnly = search.ShowActiveOnly;
        ShowFavoritesOnly = search.ShowFavoritesOnly;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task DeleteSavedSearchAsync(SavedSearch? search)
    {
        if (search == null) return;
        SavedSearches.Remove(search);
        await _savedSearchesRepo.SaveAsync(new SavedSearchSet { Searches = SavedSearches.ToList().AsReadOnly() });
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

            // 讀取（單日或跨日範圍）
            IReadOnlyList<TenderItem> rawItems;
            if (IsRangeMode)
            {
                var bag = new List<TenderItem>();
                for (var d = Date; d <= DateTo!.Value; d = d.AddDays(1))
                {
                    var snap = await _tenderRepo.LoadAsync(d, ct);
                    if (snap?.Items != null) bag.AddRange(snap.Items);
                }
                rawItems = bag;
            }
            else
            {
                var snapshot = await _tenderRepo.LoadAsync(Date, ct);
                rawItems = snapshot?.Items ?? Array.Empty<TenderItem>();
            }

            // 包裝成 ViewModel
            var wrapped = rawItems.Select(item =>
            {
                _userMarks.TryGetValue(item.SourcePk, out var mark);
                var vm = new TenderItemViewModel(item, mark);
                vm.MarkChanged += OnMarkChangedAsync;
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
            await LoadSavedSearchesCommand.ExecuteAsync(null);
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

    private async void OnMarkChangedAsync(TenderItemViewModel vm, string propertyName)
    {
        _userMarks[vm.SourcePk] = new UserMark
        {
            SourcePk = vm.SourcePk,
            IsFavorite = vm.IsFavorite,
            IsRead = vm.IsRead,
            IsExcluded = vm.IsExcluded,
            Note = vm.Note ?? string.Empty,
        };

        // 批次操作期間，跳過 disk write，由批次結束後統一寫檔
        if (_suppressMarkSideEffects) return;

        await PersistUserMarksAsync();

        // 故意不在此呼叫 ApplyFilter — 篩選結果是 snapshot，
        // 只在使用者操作上方篩選控制項時重算，資料 mark 變動不重算。
    }

    private async Task PersistUserMarksAsync()
    {
        try
        {
            var marks = new UserMarks
            {
                Marks = _userMarks.Values
                    .Where(m => m.IsFavorite || m.IsRead || m.IsExcluded || !string.IsNullOrEmpty(m.Note))
                    .ToList()
                    .AsReadOnly(),
            };
            await _userMarksRepo.SaveAsync(marks);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"儲存標記失敗：{ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleRead(TenderItemViewModel? item)
    {
        if (item != null) item.IsRead = !item.IsRead;
    }

    /// <summary>把目前篩選結果全部標記為已讀。</summary>
    [RelayCommand]
    private async Task MarkAllAsReadAsync()
    {
        await BatchToggleReadAsync(targetState: true);
    }

    /// <summary>把目前篩選結果全部標記為未讀。</summary>
    [RelayCommand]
    private async Task MarkAllAsUnreadAsync()
    {
        await BatchToggleReadAsync(targetState: false);
    }

    private async Task BatchToggleReadAsync(bool targetState)
    {
        // 暫停 side-effect，避免每筆都觸發 disk write
        _suppressMarkSideEffects = true;
        try
        {
            foreach (var vm in FilteredItems)
                if (vm.IsRead != targetState) vm.IsRead = targetState;
        }
        finally
        {
            _suppressMarkSideEffects = false;
        }

        // 統一寫檔一次；不重算篩選 (snapshot 模式)
        await PersistUserMarksAsync();
    }

    [RelayCommand]
    private void ToggleExclude(TenderItemViewModel? item)
    {
        if (item != null) item.IsExcluded = !item.IsExcluded;
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
        KeywordTitleOnly = false;
        KeywordExclude = false;
        ShowActiveOnly = false;
        ShowFavoritesOnly = false;
        ShowExcluded = false;
        ShowUnreadOnly = false;
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
        _suppressDateAutoReload = true;
        try
        {
            if (IsRangeMode)
            {
                // 跨日 mode：整段往前挪一天
                DateTo = DateTo!.Value.AddDays(-1);
                Date = Date.AddDays(-1);
            }
            else
            {
                Date = Date.AddDays(-1);
            }
        }
        finally { _suppressDateAutoReload = false; }
        await LoadAsync(ct);
    }

    [RelayCommand]
    private async Task GoNextDayAsync(CancellationToken ct)
    {
        _suppressDateAutoReload = true;
        try
        {
            if (IsRangeMode)
            {
                DateTo = DateTo!.Value.AddDays(1);
                Date = Date.AddDays(1);
            }
            else
            {
                Date = Date.AddDays(1);
            }
        }
        finally { _suppressDateAutoReload = false; }
        await LoadAsync(ct);
    }

    [RelayCommand]
    private async Task ClearDateRangeAsync(CancellationToken ct)
    {
        _suppressDateAutoReload = true;
        try { DateTo = null; }
        finally { _suppressDateAutoReload = false; }
        await LoadAsync(ct);
    }

    [RelayCommand]
    private void Print()
    {
        if (FilteredItems.Count == 0)
        {
            ErrorMessage = "目前沒有可列印的資料";
            return;
        }
        // 列印實作在 view 端（PrintHelper），這裡只觸發
        PrintRequested?.Invoke(FilteredItems);
    }

    /// <summary>view 端訂閱以執行 WPF FlowDocument 列印。</summary>
    public event Action<IReadOnlyList<TenderItemViewModel>>? PrintRequested;

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        if (FilteredItems.Count == 0)
        {
            ErrorMessage = "目前沒有可匯出的資料";
            return;
        }

        // 範圍查詢時檔名含起訖日；單日只放單一日期
        var suggested = IsRangeMode
            ? $"標案_{Date:yyyyMMdd}~{DateTo!.Value:yyyyMMdd}.xlsx"
            : $"標案_{Date:yyyyMMdd}.xlsx";
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

    [RelayCommand]
    private async Task ExportXlsmAsync(CancellationToken ct)
    {
        if (AllItems.Count == 0)
        {
            ErrorMessage = "目前沒有可匯出的資料";
            return;
        }

        var suggested = IsRangeMode
            ? $"標案_{Date:yyyyMMdd}~{DateTo!.Value:yyyyMMdd}.xlsm"
            : $"標案_{Date:yyyyMMdd}.xlsm";
        var savePath = _saveDialog.ShowSaveAsXlsm(suggested);
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            IsLoading = true;
            var allRaw      = AllItems.Select(vm => vm.Item).ToList().AsReadOnly();
            var filteredRaw = FilteredItems.Select(vm => vm.Item).ToList().AsReadOnly();
            await _xlsmExporter.ExportAsync(allRaw, filteredRaw, savePath, ct);
            ErrorMessage = $"已匯出（全部 {AllItems.Count} / 篩選 {FilteredItems.Count} 筆，含巨集）→ {savePath}";
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
    partial void OnKeywordTitleOnlyChanged(bool value) => ApplyFilter();
    partial void OnKeywordExcludeChanged(bool value) => ApplyFilter();
    partial void OnSortKeyChanged(SortKey value) => ApplyFilter();
    partial void OnSortDirectionChanged(SortDirection value) => ApplyFilter();
    partial void OnShowActiveOnlyChanged(bool value) => ApplyFilter();
    partial void OnShowFavoritesOnlyChanged(bool value) => ApplyFilter();
    partial void OnShowExcludedChanged(bool value) => ApplyFilter();
    partial void OnShowUnreadOnlyChanged(bool value) => ApplyFilter();
    partial void OnDateToChanged(DateOnly? value)
    {
        OnPropertyChanged(nameof(IsRangeMode));
        if (!_suppressDateAutoReload)
            _ = LoadCommand.ExecuteAsync(null);
    }
    partial void OnDateChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(IsRangeMode));
        if (!_suppressDateAutoReload)
            _ = LoadCommand.ExecuteAsync(null);
    }

    /// <summary>原子地設定起／訖日；批次操作避免每個 setter 都觸發 reload。</summary>
    public void SetDates(DateOnly date, DateOnly? dateTo)
    {
        _suppressDateAutoReload = true;
        try
        {
            Date = date;
            DateTo = dateTo;
        }
        finally
        {
            _suppressDateAutoReload = false;
        }
    }
    partial void OnSelectedItemChanged(TenderItemViewModel? value)
    {
        // 點選任一列即視為已讀；snapshot 模式下不會觸發重新篩選，
        // 即便「只看未讀」開著，被點過的列也會留在當前快照裡，下次動到上方篩選才會消失。
        if (value is not null && !value.IsRead)
            value.IsRead = true;
    }
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
            KeywordTitleOnly = KeywordTitleOnly,
            KeywordExclude = KeywordExclude,
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

        // 把過濾結果 sourcePk 對回 ViewModel（保留 IsFavorite 等狀態），加上使用者標記過濾
        var vmByPk = AllItems.ToDictionary(v => v.SourcePk);
        var filtered = rawFiltered
            .Select(item => vmByPk[item.SourcePk])
            .Where(vm => ShowExcluded || !vm.IsExcluded)
            .Where(vm => !ShowFavoritesOnly || vm.IsFavorite)
            .Where(vm => !ShowUnreadOnly || !vm.IsRead)
            .ToList()
            .AsReadOnly();

        FilteredItems = filtered;
    }
}
