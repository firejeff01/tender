using Tender.Core.Exceptions;
using Tender.Core.Models;
using Tender.Storage.Repositories;

namespace Tender.Desktop.Services;

public sealed class MonthlyCalendarService : IMonthlyCalendarService
{
    private readonly IDailySummaryRepository _summaryRepo;

    public MonthlyCalendarService(IDailySummaryRepository summaryRepo)
    {
        _summaryRepo = summaryRepo;
    }

    public async Task<MonthlyCalendarView> LoadMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDay = new DateOnly(year, month, 1);
        // Sunday=0, Monday=1, ..., Saturday=6 — 行事曆以週日開頭排版
        var leadingPlaceholders = (int)firstDay.DayOfWeek;

        var days = new List<MonthlyCalendarDay>(leadingPlaceholders + daysInMonth);

        // 月初前的占位格，讓「日」排到第一欄
        for (int i = 0; i < leadingPlaceholders; i++)
        {
            days.Add(new MonthlyCalendarDay
            {
                Date = firstDay.AddDays(-(leadingPlaceholders - i)),
                Summary = null,
                IsCorrupted = false,
                IsPlaceholder = true,
            });
        }

        var monthlyTotal = 0;
        for (int d = 1; d <= daysInMonth; d++)
        {
            ct.ThrowIfCancellationRequested();
            var date = new DateOnly(year, month, d);
            var day = await ReadDayAsync(date, ct);
            if (day.Summary != null)
                monthlyTotal += day.Summary.TotalCount;
            days.Add(day);
        }

        return new MonthlyCalendarView
        {
            Year = year,
            Month = month,
            Days = days.AsReadOnly(),
            MonthlyTotalCount = monthlyTotal,
        };
    }

    public Task<MonthlyCalendarDay> RefreshDayAsync(DateOnly date, CancellationToken ct = default)
        => ReadDayAsync(date, ct);

    private async Task<MonthlyCalendarDay> ReadDayAsync(DateOnly date, CancellationToken ct)
    {
        try
        {
            var summary = await _summaryRepo.LoadAsync(date, ct);
            return new MonthlyCalendarDay
            {
                Date = date,
                Summary = summary,
                IsCorrupted = false,
            };
        }
        catch (CorruptedDataException)
        {
            return new MonthlyCalendarDay
            {
                Date = date,
                Summary = null,
                IsCorrupted = true,
            };
        }
    }
}
