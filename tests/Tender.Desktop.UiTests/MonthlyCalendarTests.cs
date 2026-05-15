using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Tender.Desktop.UiTests.Infrastructure;
using Xunit;

namespace Tender.Desktop.UiTests;

/// <summary>
/// MonthlyCalendarView smoke：行事曆是 ShellWindow 的主內容（IsDailyQueryVisible=false 時顯示）。
/// 月份切換按鈕能找到並 invokable 即視為通過。
/// </summary>
[Collection("UiTests")]
public sealed class MonthlyCalendarTests
{
    private readonly AppFixture _f;
    public MonthlyCalendarTests(AppFixture f) => _f = f;

    [Fact]
    public void MonthNavigation_PrevAndNextButtons_ArePresent()
    {
        var prev = _f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("GoPreviousMonthButton"))?.AsButton();
        var next = _f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("GoNextMonthButton"))?.AsButton();
        var thisMonth = _f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("GoToCurrentMonthButton"))?.AsButton();

        prev.Should().NotBeNull();
        next.Should().NotBeNull();
        thisMonth.Should().NotBeNull();
    }

    [Fact]
    public void GoPreviousMonth_ThenGoToCurrent_DoesNotCrash()
    {
        var prev = _f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("GoPreviousMonthButton")).AsButton();
        var thisMonth = _f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("GoToCurrentMonthButton")).AsButton();

        prev.Invoke();
        // load is async; 用 wait until idle 等 UI 穩定
        Thread.Sleep(500);
        thisMonth.Invoke();
        Thread.Sleep(500);

        // 主視窗仍可用即視為過
        _f.MainWindow.IsAvailable.Should().BeTrue();
    }
}
