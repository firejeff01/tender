using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Models;
using Tender.Desktop.Services;
using System.Linq;

namespace Tender.Desktop.ViewModels;

public partial class MonthlyCalendarViewModel : ObservableObject
{
    private readonly IMonthlyCalendarService _service;

    [ObservableProperty]
    private int _year;

    [ObservableProperty]
    private int _month;

    [ObservableProperty]
    private MonthlyCalendarView? _view;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // 月份概況（隨 View 變動自動更新）
    [ObservableProperty]
    private int _daysWithDataCount;

    [ObservableProperty]
    private int _failedDaysCount;

    partial void OnViewChanged(MonthlyCalendarView? value)
    {
        if (value == null)
        {
            DaysWithDataCount = 0;
            FailedDaysCount = 0;
            return;
        }
        // 「有資料」= 爬蟲跑過 (Summary 非 null) 且實際抓到至少 1 筆。
        // 0 筆即使爬蟲成功跑完也視同無資料（如假日或 gov 網站當日無公告）。
        DaysWithDataCount = value.Days.Count(d => d.Summary != null && d.Summary.TotalCount > 0);
        FailedDaysCount = value.Days.Count(d => d.Summary?.LastRunStatus == RunStatus.Failed);
    }

    public MonthlyCalendarViewModel(IMonthlyCalendarService service)
    {
        _service = service;
        var today = DateTime.Today;
        _year = today.Year;
        _month = today.Month;
    }

    [RelayCommand]
    private async Task LoadMonthAsync(CancellationToken ct)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            View = await _service.LoadMonthAsync(Year, Month, ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoPreviousMonthAsync(CancellationToken ct)
    {
        var date = new DateOnly(Year, Month, 1).AddMonths(-1);
        Year = date.Year;
        Month = date.Month;
        await LoadMonthAsync(ct);
    }

    [RelayCommand]
    private async Task GoNextMonthAsync(CancellationToken ct)
    {
        var date = new DateOnly(Year, Month, 1).AddMonths(1);
        Year = date.Year;
        Month = date.Month;
        await LoadMonthAsync(ct);
    }

    [RelayCommand]
    private async Task GoToCurrentMonthAsync(CancellationToken ct)
    {
        var today = DateTime.Today;
        Year = today.Year;
        Month = today.Month;
        await LoadMonthAsync(ct);
    }
}
