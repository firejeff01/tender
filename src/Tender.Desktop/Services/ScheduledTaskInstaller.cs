using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
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

        // 改用 XML import：避免 schtasks /TR 把整段含引號字串塞進 <Command>
        // 使 Task Scheduler 找不到 exe，且預設 DisallowStartIfOnBatteries=true
        // 會讓筆電拔電後永遠卡 Queued。XML 內可一次處理這兩件事。
        var xmlPath = Path.Combine(Path.GetTempPath(), $"TenderSearch.{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, BuildTaskXml(crawlerExe, scheduledTime), Encoding.Unicode);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F",
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
        finally
        {
            try { if (File.Exists(xmlPath)) File.Delete(xmlPath); } catch { /* ignore */ }
        }
    }

    private static string BuildTaskXml(string crawlerExe, string hhmm)
    {
        // StartBoundary 必須是 ISO-8601 local time；Task Scheduler 只看 HH:mm 部分做每日觸發
        var startBoundary = $"{DateTime.Today:yyyy-MM-dd}T{hhmm}:00";
        var escapedExe = SecurityElement.Escape(crawlerExe);

        // 設定要點：
        //   DisallowStartIfOnBatteries=false：筆電拔電也要跑（schtasks CLI 預設為 true）
        //   StopIfGoingOnBatteries=false：跑到一半切到電池不要殺
        //   StartWhenAvailable=true：錯過時段（例如電腦睡眠）下次喚醒立刻補跑
        //   MultipleInstancesPolicy=IgnoreNew：避免重複觸發
        //   RunLevel=LeastPrivilege：受限完整性，避免提權
        //   LogonType=InteractiveToken：以登入使用者身份執行
        //   <Command> bare path、<Arguments> 分離：避免 schtasks /TR 的引號 bug
        return
$@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>TenderSearch daily tender crawl</Description>
  </RegistrationInfo>
  <Triggers>
    <CalendarTrigger>
      <StartBoundary>{startBoundary}</StartBoundary>
      <Enabled>true</Enabled>
      <ScheduleByDay>
        <DaysInterval>1</DaysInterval>
      </ScheduleByDay>
    </CalendarTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>
    <Priority>7</Priority>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedExe}</Command>
      <Arguments>--mode scheduled</Arguments>
    </Exec>
  </Actions>
</Task>";
    }
}
