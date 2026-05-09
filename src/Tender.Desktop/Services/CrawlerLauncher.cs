using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Tender.Core.Models;

namespace Tender.Desktop.Services;

public sealed class CrawlerLauncher : ICrawlerLauncher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<int> LaunchAsync(
        CrawlerMode mode,
        DateOnly targetDate,
        IProgress<CrawlerProgressEvent>? progress,
        CancellationToken ct = default)
    {
        var exe = FindCrawlerExe()
            ?? throw new FileNotFoundException(
                "找不到 Tender.Crawler.exe；預期同目錄或開發路徑下。");

        var modeStr = mode switch
        {
            CrawlerMode.Scheduled  => "scheduled",
            CrawlerMode.Catchup    => "catchup",
            CrawlerMode.Manual     => "manual",
            CrawlerMode.ManualRedo => "manual-redo",
            CrawlerMode.Poc        => "poc",
            _                      => "manual",
        };

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--mode {modeStr} --target-date {targetDate:yyyy-MM-dd}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Process.Start returned null.");

        // 同時讀 stdout (progress) 與 stderr (warnings/retries 不影響邏輯)
        var stdoutTask = ReadStdoutAsync(proc.StandardOutput, progress, ct);
        var stderrTask = DrainAsync(proc.StandardError, ct);

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        await stdoutTask;
        await stderrTask;

        return proc.ExitCode;
    }

    private static async Task ReadStdoutAsync(
        StreamReader stream,
        IProgress<CrawlerProgressEvent>? progress,
        CancellationToken ct)
    {
        string? line;
        while ((line = await stream.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || progress == null) continue;
            // 只解析以 '{' 開頭的行（其餘 stdout 內容忽略）
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith('{')) continue;

            try
            {
                var evt = JsonSerializer.Deserialize<CrawlerProgressEvent>(trimmed, JsonOptions);
                if (evt != null) progress.Report(evt);
            }
            catch (JsonException)
            {
                // 非 ProgressEvent 的 JSON 行，忽略
            }
        }
    }

    private static async Task DrainAsync(StreamReader stream, CancellationToken ct)
    {
        // 不解析 stderr，只把它讀完避免 buffer 卡死
        while (await stream.ReadLineAsync(ct) != null) { }
    }

    /// <summary>
    /// 在多個已知位置尋找 Tender.Crawler.exe：
    ///   1. 與 Desktop 同資料夾（極簡部署）
    ///   2. 兄弟資料夾 ../crawler/Tender.Crawler.exe（MSI 安裝結構）
    ///   3. dev 環境相對路徑
    /// </summary>
    private static string? FindCrawlerExe()
    {
        var baseDir = AppContext.BaseDirectory;

        // 1. 同資料夾
        var sameFolder = Path.Combine(baseDir, "Tender.Crawler.exe");
        if (File.Exists(sameFolder)) return sameFolder;

        // 2. MSI 安裝結構：Program Files/TenderSearch/desktop + crawler 兄弟資料夾
        var siblingCrawler = Path.GetFullPath(Path.Combine(baseDir, "..", "crawler", "Tender.Crawler.exe"));
        if (File.Exists(siblingCrawler)) return siblingCrawler;

        // 3. dev 環境：src/Tender.Desktop/bin/<config>/net8.0-windows → src/Tender.Crawler/bin/<config>/net8.0
#if DEBUG
        const string config = "Debug";
#else
        const string config = "Release";
#endif
        var devPath = Path.GetFullPath(Path.Combine(
            baseDir, "..", "..", "..", "..",
            "Tender.Crawler", "bin", config, "net8.0", "Tender.Crawler.exe"));
        if (File.Exists(devPath)) return devPath;

        return null;
    }
}
