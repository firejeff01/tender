using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FluentAssertions;
using Tender.Desktop.UiTests.Infrastructure;
using Xunit;

namespace Tender.Desktop.UiTests;

/// <summary>
/// ShellWindow smoke：標題 + 設定 toggle 存在 + 可開／可關。
/// 不直接驗 popup 內容（WPF Popup 在 UIA3 找子元素不穩，留給 KeywordsManageWindowTests 從另一條路驗）。
/// </summary>
[Collection("UiTests")]
public sealed class ShellWindowTests
{
    private readonly AppFixture _f;
    public ShellWindowTests(AppFixture f) => _f = f;

    [Fact]
    public void MainWindow_TitleContainsExpected()
    {
        _f.MainWindow.Title.Should().Contain("標案查詢");
    }

    [Fact]
    public void SettingsMenuButton_IsPresentAndToggleable()
    {
        var toggle = _f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId("SettingsMenuButton"))?.AsToggleButton();
        toggle.Should().NotBeNull("應該找得到設定 ToggleButton");

        // 切換狀態應改變
        var beforeState = toggle!.ToggleState;
        toggle.Toggle();
        toggle.ToggleState.Should().NotBe(beforeState);
        // 收尾：切回去避免污染後續測試
        toggle.Toggle();
    }
}
