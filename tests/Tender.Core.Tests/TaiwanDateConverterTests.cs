using FluentAssertions;
using Tender.Core.DateConversion;
using Xunit;

namespace Tender.Core.Tests;

public sealed class TaiwanDateConverterTests
{
    private readonly TaiwanDateConverter _sut = new();

    // ---- RocToDateOnly ----

    [Theory]
    [InlineData("115/05/08", 2026, 5, 8)]
    [InlineData("112/01/01", 2023, 1, 1)]
    [InlineData("111/12/31", 2022, 12, 31)]
    [InlineData("114/02/29", 2025, 2, 29)] // 2025 非閏年，應回傳 null
    [InlineData("113/02/29", 2024, 2, 29)] // 2024 是閏年，應成功
    public void RocToDateOnly_ValidInput(string roc, int year, int month, int day)
    {
        // 只有非閏年的 2/29 才回傳 null，其餘應回傳正確值
        var result = _sut.RocToDateOnly(roc);
        if (year == 2025 && month == 2 && day == 29)
        {
            result.Should().BeNull("2025 is not a leap year");
        }
        else
        {
            result.Should().Be(new DateOnly(year, month, day));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("115/13/08")]   // 月份超出範圍
    [InlineData("115/00/08")]   // 月份為 0
    [InlineData("115/05/32")]   // 日期超出範圍
    [InlineData("abc/05/08")]   // 非數字年份
    [InlineData("115-05-08")]   // 錯誤分隔符
    [InlineData("11505/08")]    // 格式錯誤
    public void RocToDateOnly_InvalidInput_ReturnsNull(string roc)
    {
        _sut.RocToDateOnly(roc).Should().BeNull();
    }

    [Fact]
    public void RocToDateOnly_NullInput_ReturnsNull()
    {
        _sut.RocToDateOnly(null!).Should().BeNull();
    }

    // ---- DateOnlyToRoc ----

    [Theory]
    [InlineData(2026, 5, 8, "115/05/08")]
    [InlineData(2023, 1, 1, "112/01/01")]
    [InlineData(2022, 12, 31, "111/12/31")]
    [InlineData(2024, 2, 29, "113/02/29")]   // 閏年
    [InlineData(2000, 1, 1, "89/01/01")]    // 較早年份（ROC 89 年）
    public void DateOnlyToRoc_ValidInput_ReturnsCorrectRoc(int year, int month, int day, string expected)
    {
        var date = new DateOnly(year, month, day);
        _sut.DateOnlyToRoc(date).Should().Be(expected);
    }

    // ---- 雙向轉換 round-trip ----

    [Theory]
    [InlineData(2026, 5, 8)]
    [InlineData(2024, 2, 29)]   // 閏年
    [InlineData(2022, 12, 31)]
    [InlineData(2000, 1, 1)]
    public void RoundTrip_DateOnly_ToRoc_ToDateOnly(int year, int month, int day)
    {
        var original = new DateOnly(year, month, day);
        var roc = _sut.DateOnlyToRoc(original);
        var restored = _sut.RocToDateOnly(roc);
        restored.Should().Be(original);
    }

    // ---- IsRocDateInRange ----

    [Fact]
    public void IsRocDateInRange_InRange_ReturnsTrue()
    {
        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 5, 31);
        _sut.IsRocDateInRange("115/05/08", from, to).Should().BeTrue();
    }

    [Fact]
    public void IsRocDateInRange_AtBoundary_ReturnsTrue()
    {
        var from = new DateOnly(2026, 5, 8);
        var to = new DateOnly(2026, 5, 8);
        _sut.IsRocDateInRange("115/05/08", from, to).Should().BeTrue();
    }

    [Fact]
    public void IsRocDateInRange_OutOfRange_ReturnsFalse()
    {
        var from = new DateOnly(2026, 5, 9);
        var to = new DateOnly(2026, 5, 31);
        _sut.IsRocDateInRange("115/05/08", from, to).Should().BeFalse();
    }

    [Fact]
    public void IsRocDateInRange_InvalidRocDate_ReturnsFalse()
    {
        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 5, 31);
        _sut.IsRocDateInRange("invalid", from, to).Should().BeFalse();
    }
}
