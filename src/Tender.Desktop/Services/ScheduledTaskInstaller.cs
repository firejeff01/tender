using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Tender.Desktop.Services;

public sealed class ScheduledTaskInstaller : IScheduledTaskInstaller
{
    private const string TaskName = "TenderSearch.DailyCrawl";
    private static readonly Regex HhmmRegex = new(@"^\d{2}:\d{2}$", RegexOptions.Compiled);

    public bool EnsureTask(string scheduledTime)
    {
        if (!HhmmRegex.IsMatch(scheduledTime)) return false;

        var crawlerExe = CrawlerLauncher.FindCrawlerExe();
        if (crawlerExe is null) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                // /RL LIMITED：以受限完整性執行，避免提權；
                // 不帶 /RU 即以呼叫者帳號（目前桌面登入使用者）建立。
                Arguments =
                    $"/Create /SC DAILY /TN \"{TaskName}\" " +
                    $"/TR \"\\\"{crawlerExe}\\\" --mode scheduled\" " +
                    $"/ST {scheduledTime} /RL LIMITED /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
