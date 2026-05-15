using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Tender.Desktop.UiTests.Infrastructure;
using Xunit;

namespace Tender.Desktop.UiTests;

/// <summary>
/// KeywordsManageWindow（篩選類別設定）的 smoke。
///
/// **目前全部 Skip**：在 2026-05-15 的 FlaUI 5 / UIA3 + WPF .NET 10 組合下，
/// 從 UIA `Invoke` 觸發 ICommand → 內部 `Window.ShowDialog()` 不會讓子視窗對 UIA 顯現，
/// 即使 popup 旁路（test backdoor button）也一樣（試過 popup click / mouse Click /
/// `StaysOpen=True` / 非零 size 的 hidden 按鈕，5 種變化都不過）。
///
/// 邏輯層覆蓋已在 Tender.Desktop.Tests 的 KeywordsManageViewModelTests 完成（29 個）。
/// UI 互動驗證留給人工 smoke checklist 直到找到突破口（可能方向：改 Show() 非 modal、
/// 用 WindowsAppDriver 取代 FlaUI、或在測試模式下完全 bypass dialog）。
/// </summary>
[Collection("UiTests")]
public sealed class KeywordsManageWindowTests
{
    private const string SkipReason =
        "FlaUI/UIA3 在這個專案 ShowDialog from IAsyncRelayCommand 路徑下找不到子視窗 — see class doc.";

    private readonly AppFixture _f;
    public KeywordsManageWindowTests(AppFixture f) => _f = f;

    private Window Open() => WindowOpener.OpenViaTestBackdoor(
        _f, "TestModeOpenKeywordsManagerButton", "篩選類別設定");

    [Fact(Skip = SkipReason)]
    public void Opens_WithDefaultGroups_Loaded()
    {
        var win = Open();
        try
        {
            win.Title.Should().Contain("篩選類別");

            var listbox = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("GroupsListBox")).AsListBox();
            listbox.Should().NotBeNull();
            // 預設應載入 9 個群組
            listbox.Items.Length.Should().Be(9);
        }
        finally { win.Close(); }
    }

    [Fact(Skip = SkipReason)]
    public void AddGroupButton_AppendsNewGroup()
    {
        var win = Open();
        try
        {
            var listbox = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("GroupsListBox")).AsListBox();
            var initialCount = listbox.Items.Length;

            var addBtn = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("AddGroupButton")).AsButton();
            addBtn.Invoke();

            listbox.Items.Length.Should().Be(initialCount + 1);
        }
        finally { win.Close(); }
    }

    [Fact(Skip = SkipReason)]
    public void MergeDefaultsButton_IsPresent_AndInvokable()
    {
        var win = Open();
        try
        {
            var mergeBtn = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("MergeDefaultsButton")).AsButton();
            mergeBtn.Should().NotBeNull();
            mergeBtn.IsEnabled.Should().BeTrue();
            // 預設狀態下「已是最新」，Invoke 不應拋例外、視窗應仍存在
            mergeBtn.Invoke();
            win.IsAvailable.Should().BeTrue();
        }
        finally { win.Close(); }
    }

    [Fact(Skip = SkipReason)]
    public void SaveButton_IsPresent_AndInvokable()
    {
        var win = Open();
        try
        {
            var saveBtn = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("SaveButton")).AsButton();
            saveBtn.Should().NotBeNull();
            saveBtn.IsEnabled.Should().BeTrue();
            saveBtn.Invoke();
            win.IsAvailable.Should().BeTrue();
        }
        finally { win.Close(); }
    }

    [Fact(Skip = SkipReason)]
    public void GroupMoveDownButton_ChangesFirstGroupOrder()
    {
        var win = Open();
        try
        {
            var listbox = win.FindFirstDescendant(cf =>
                cf.ByAutomationId("GroupsListBox")).AsListBox();
            var firstItemNameBefore = listbox.Items[0].FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))?.AsTextBox()?.Text;

            // 找到第一列裡的 ↓ 按鈕（同一 row 內）
            var firstRowDown = listbox.Items[0].FindFirstDescendant(cf =>
                cf.ByAutomationId("GroupMoveDownButton")).AsButton();
            firstRowDown.Invoke();

            var firstItemNameAfter = listbox.Items[0].FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))?.AsTextBox()?.Text;

            firstItemNameAfter.Should().NotBe(firstItemNameBefore,
                "下移第一列後，新的第一列名稱應該不同");
        }
        finally { win.Close(); }
    }
}
