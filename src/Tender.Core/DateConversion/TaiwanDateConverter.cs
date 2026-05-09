namespace Tender.Core.DateConversion;

/// <summary>
/// 民國年（ROC/Taiwan calendar）與西元年互轉。
/// ROC 年 = 西元年 - 1911。
/// 格式：「115/05/08」（民國年/月/日）。
/// </summary>
public sealed class TaiwanDateConverter : ITaiwanDateConverter
{
    private const int RocYearOffset = 1911;

    /// <inheritdoc/>
    public DateOnly? RocToDateOnly(string rocDate)
    {
        if (string.IsNullOrWhiteSpace(rocDate))
            return null;

        var parts = rocDate.Split('/');
        if (parts.Length != 3)
            return null;

        if (!int.TryParse(parts[0], out int rocYear) ||
            !int.TryParse(parts[1], out int month) ||
            !int.TryParse(parts[2], out int day))
            return null;

        int year = rocYear + RocYearOffset;

        try
        {
            return new DateOnly(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string DateOnlyToRoc(DateOnly date)
    {
        int rocYear = date.Year - RocYearOffset;
        return $"{rocYear}/{date.Month:D2}/{date.Day:D2}";
    }

    /// <inheritdoc/>
    public bool IsRocDateInRange(string rocDate, DateOnly from, DateOnly to)
    {
        var date = RocToDateOnly(rocDate);
        if (date is null)
            return false;

        return date.Value >= from && date.Value <= to;
    }
}
