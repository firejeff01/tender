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
        var days = new List<MonthlyCalendarDay>(daysInMonth);
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
