using FlaUI.Core.AutomationElements;

namespace Tender.Desktop.UiTests.Infrastructure;

/// <summary>
/// 從 ShellWindow 開啟設定 popup → 點按鈕開子視窗的共用流程。
/// 每個測試用完後務必關閉回傳的 Window，避免污染同一 AppFixture 的後續測試。
/// </summary>
public static class WindowOpener
{
    /// <summary>
    /// 用 ShellWindow 內的 test backdoor 按鈕（TENDER_TEST_MODE=1 才顯示）直接觸發 OpenXxxCommand，
    /// 等到 title 含 expectedTitleSubstring 的 top-level window 出現。
    ///
    /// 為什麼要 backdoor：WPF Popup 對 UIA3 不友善 —— 從 popup 內的 Button 用 Invoke / Click 都無法可靠觸發
    /// 它的 Command（popup 雖然 visible 但 button 的 click chain 不到 Command）。直接點 main window 上
    /// 的 hidden test button (Width=0 Height=0 但 IsHitTestVisible=true，UIA 還找得到) 走原本的 ICommand 是穩定的。
    /// </summary>
    public static Window OpenViaTestBackdoor(AppFixture f, string testButtonAutomationId, string expectedTitleSubstring)
    {
        var btn = f.MainWindow.FindFirstDescendant(cf =>
            cf.ByAutomationId(testButtonAutomationId))?.AsButton();
        if (btn is null)
            throw new InvalidOperationException(
                $"找不到 test backdoor button '{testButtonAutomationId}' — TENDER_TEST_MODE 是否設好？");

        btn.Invoke();

        var window = WaitFor(() =>
        {
            foreach (var w in f.Application.GetAllTopLevelWindows(f.Automation))
            {
                if (w == f.MainWindow) continue;
                if (!string.IsNullOrEmpty(w.Title) && w.Title.Contains(expectedTitleSubstring))
                    return w;
            }
            return null;
        }, TimeSpan.FromSeconds(10));

        if (window is null)
            throw new InvalidOperationException(
                $"子視窗（title 含 '{expectedTitleSubstring}'）未在 10 秒內開啟");
        return window;
    }

    /// <summary>輪詢 supplier 直到回傳非 null 或 deadline 到。</summary>
    private static T? WaitFor<T>(Func<T?> supplier, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var v = supplier();
                if (v is not null) return v;
            }
            catch { /* race conditions, ignore and retry */ }
            Thread.Sleep(150);
        }
        return null;
    }
}
