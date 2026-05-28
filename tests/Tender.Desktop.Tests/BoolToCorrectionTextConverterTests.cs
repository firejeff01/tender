using System.Globalization;
using FluentAssertions;
using Tender.Desktop.Converters;
using Xunit;

namespace Tender.Desktop.Tests;

public sealed class BoolToCorrectionTextConverterTests
{
    private readonly BoolToCorrectionTextConverter _sut = new();

    [Fact]
    public void Convert_True_ReturnsCorrectionText()
    {
        var result = _sut.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(" (更正公告)");
    }

    [Fact]
    public void Convert_False_ReturnsEmpty()
    {
        var result = _sut.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }

    [Fact]
    public void Convert_Null_ReturnsEmpty()
    {
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }
}
