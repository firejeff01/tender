using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Tender.Desktop.UiTests.Infrastructure;
using Xunit;

namespace Tender.Desktop.UiTests;

/// <summary>
/// AppSettingsWindow smoke。
///
/// **Skip**：跟 KeywordsManageWindowTests 同一個結構性問題 —
/// FlaUI/UIA3 在這個專案 ShowDialog from IAsyncRelayCommand 路徑下找不到子視窗。
/// </summary>
[Collection("UiTests")]
public sealed class AppSettingsWindowTests
{
    private readonly AppFixture _f;
    public AppSettingsWindowTests(AppFixture f) => _f = f;

    [Fact(Skip = "ShowDialog from IAsyncRelayCommand 在 FlaUI/UIA3 下找不到子視窗，see KeywordsManageWindowTests doc.")]
    public void Opens_AndHasSaveButton()
    {
        var win = WindowOpener.OpenViaTestBackdoor(_f, "TestModeOpenAppSettingsButton", "應用設定");
        try
        {
            var save = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("SaveButton"))?.AsButton();
            save.Should().NotBeNull();
            save.IsEnabled.Should().BeTrue();
        }
        finally { win.Close(); }
    }
}
