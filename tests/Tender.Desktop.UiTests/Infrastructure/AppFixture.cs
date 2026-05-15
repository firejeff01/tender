using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Tender.Desktop.UiTests.Infrastructure;

/// <summary>
/// 整個 UiTests assembly 共用一個 AppFixture（透過 <see cref="UiTestsCollection"/>）：
///   - 啟動 Tender.Desktop.exe，設 TENDER_DATA_ROOT_OVERRIDE 指向隔離目錄
///   - TENDER_TEST_MODE=1 跳過 scheduled task / missed run / update check
///   - 測試 assembly 結束時 dispose 砍 app 與臨時目錄
///
/// 為什麼要共用：FlaUI 5 / UIA3 在同一 testhost process 裡多次 Launch+Dispose Application
/// 時會出現 COM HRESULT 錯誤（UIA3 內部 COM 狀態殘留），改用單一 app 解掉這個雷。
///
/// 代價：所有測試共用同一 app instance。每個測試要負責清掉自己開過的子視窗（modal dialog 等），
/// 不可在 main window 留下後續測試會打架的狀態。
/// </summary>
public sealed class AppFixture : IDisposable
{
    public Application Application { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }
    public string DataRoot { get; }

    public AppFixture()
    {
        DataRoot = Path.Combine(Path.GetTempPath(), "tender-ui-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(DataRoot);

        var exePath = LocateDesktopExe();
        var psi = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
        };
        psi.Environment["TENDER_DATA_ROOT_OVERRIDE"] = DataRoot;
        psi.Environment["TENDER_TEST_MODE"] = "1";

        Application = Application.Launch(psi);
        Automation = new UIA3Automation();
        MainWindow = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(30))
                     ?? throw new InvalidOperationException("無法在 30 秒內取得主視窗");
    }

    public void Dispose()
    {
        // 先 dispose automation（COM 相依，留到最後做容易讓下一個 fixture 起手時拿到髒狀態）
        try { Automation.Dispose(); } catch { }

        // 不用 Close（app 有 NotifyIcon tray，WM_CLOSE 會被 tray icon hold 住，留下孤兒程序）。
        // 透過 ProcessId 取得 OS process 強制 Kill，再等實際結束，確保下一個 fixture 起時環境乾淨。
        try
        {
            var proc = Process.GetProcessById(Application.ProcessId);
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
        }
        catch { /* 已不存在或 race 都當清掉了 */ }
        try { Application.Dispose(); } catch { }

        try { Directory.Delete(DataRoot, recursive: true); } catch { }
    }

    private static string LocateDesktopExe()
    {
        var testAsmDir = Path.GetDirectoryName(typeof(AppFixture).Assembly.Location)
                         ?? AppContext.BaseDirectory;

        // 候選 1：測試 bin 同層（若有 copy）
        var same = Path.Combine(testAsmDir, "Tender.Desktop.exe");
        if (File.Exists(same)) return same;

        // 候選 2：repo 結構推回去
        // testAsmDir = repo\tests\Tender.Desktop.UiTests\bin\<Config>\net10.0-windows
        // exe       = repo\src\Tender.Desktop\bin\<Config>\net10.0-windows\Tender.Desktop.exe
        var config = new DirectoryInfo(testAsmDir).Parent?.Name ?? "Debug";
        var repoRoot = Path.GetFullPath(Path.Combine(testAsmDir, "..", "..", "..", "..", ".."));
        var fromRepo = Path.Combine(repoRoot,
            "src", "Tender.Desktop", "bin", config, "net10.0-windows", "Tender.Desktop.exe");
        if (File.Exists(fromRepo)) return fromRepo;

        throw new FileNotFoundException(
            $"找不到 Tender.Desktop.exe；試過：\n  {same}\n  {fromRepo}");
    }
}
