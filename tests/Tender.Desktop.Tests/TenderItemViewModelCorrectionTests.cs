using FluentAssertions;
using Tender.Core.Models;
using Tender.Desktop.ViewModels;
using Xunit;

namespace Tender.Desktop.Tests;

public sealed class TenderItemViewModelCorrectionTests
{
    private static TenderItem MakeItem(string tenderName) => new()
    {
        SourcePk = "pk1",
        AgencyName = "測試機關",
        TenderName = tenderName,
        TenderMethod = "公開招標",
        AnnouncementDate = "115/05/28",
        DetailUrl = "https://example.com",
        CreatedAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void IsCorrection_True_WhenNameContainsCorrectionTag()
    {
        var vm = new TenderItemViewModel(MakeItem("某某工程案(更正公告)"));

        vm.IsCorrection.Should().BeTrue();
    }

    [Fact]
    public void IsCorrection_False_WhenNameDoesNotContainTag()
    {
        var vm = new TenderItemViewModel(MakeItem("某某工程案"));

        vm.IsCorrection.Should().BeFalse();
    }

    [Fact]
    public void DisplayTenderName_StripsCorrectionTag()
    {
        var vm = new TenderItemViewModel(MakeItem("某某工程案(更正公告)"));

        vm.DisplayTenderName.Should().Be("某某工程案");
    }

    [Fact]
    public void DisplayTenderName_UnchangedWhenNoTag()
    {
        var vm = new TenderItemViewModel(MakeItem("某某工程案"));

        vm.DisplayTenderName.Should().Be("某某工程案");
    }

    [Fact]
    public void DisplayTenderName_HandlesTagInMiddle()
    {
        var vm = new TenderItemViewModel(MakeItem("前段(更正公告)後段"));

        vm.IsCorrection.Should().BeTrue();
        vm.DisplayTenderName.Should().Be("前段後段");
    }
}
