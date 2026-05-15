using FluentAssertions;
using Tender.Core.Models;
using Xunit;

namespace Tender.Core.Tests;

public sealed class KeywordGroupColorsTests
{
    [Fact]
    public void Palette_Has9DistinctColors()
    {
        KeywordGroupColors.Palette.Should().HaveCount(9);
        KeywordGroupColors.Palette.Distinct().Should().HaveCount(9);
    }

    [Fact]
    public void Palette_AllAreHexStrings()
    {
        foreach (var color in KeywordGroupColors.Palette)
        {
            color.Should().StartWith("#");
            color.Length.Should().Be(7, $"color '{color}' 應為 #RRGGBB 七字元");
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    [InlineData(9, 0)]   // wrap once
    [InlineData(17, 8)]  // wrap once
    [InlineData(18, 0)]  // wrap twice
    public void GetByIndex_WrapsAroundForPositiveIndex(int index, int expectedPaletteIndex)
    {
        KeywordGroupColors.GetByIndex(index)
            .Should().Be(KeywordGroupColors.Palette[expectedPaletteIndex]);
    }

    [Theory]
    [InlineData(-1, 8)]  // negative wraps to tail
    [InlineData(-9, 0)]
    [InlineData(-10, 8)]
    public void GetByIndex_HandlesNegativeIndexGracefully(int index, int expectedPaletteIndex)
    {
        KeywordGroupColors.GetByIndex(index)
            .Should().Be(KeywordGroupColors.Palette[expectedPaletteIndex]);
    }
}
