using System.Net;
using AngleSharp;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.DependencyInjection;
using Tender.Core.Clock;
using Tender.Core.Constants;
using Tender.Core.DateConversion;
using Tender.Core.Exceptions;
using Tender.Core.Keywords;
using Tender.Core.Models;
using Tender.Crawler.Application;
using Tender.Crawler.Parsing;
using Tender.Crawler.Reporting;
using Tender.Crawler.Spider;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Crawler;

/// <summary>
/// Tender.Crawler.exe 入口。
/// Exit Code：
///   0 = success
///   1 = network failure
///   2 = parse failure
///   3 = io failure
///   4 = locked（另一個 run 進行中）
///   5 = invalid args
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var mode = GetArg(args, "--mode") ?? "manual";
        var targetDateStr = GetArg(args, "--target-date") ?? DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var engineOverride = GetArg(args, "--engine");  // reserved: "httpclient" | "playwright"

        if (!DateOnly.TryParseExact(targetDateStr, "yyyy-MM-dd", out var targetDate))
        {
            Console.Error.WriteLine($"Invalid --target-date: {targetDateStr}. Expected format: yyyy-MM-dd");
            return 5;
        }

        // 解析模式
        CrawlerMode crawlerMode;
        switch (mode.ToLowerInvariant())
        {
            case "scheduled":   crawlerMode = CrawlerMode.Scheduled;  break;
            case "catchup":     crawlerMode = CrawlerMode.Catchup;    break;
            case "manual":      crawlerMode = CrawlerMode.Manual;     break;
            case "manual-redo": crawlerMode = CrawlerMode.ManualRedo; break;
            case "poc":         return await RunPocAsync(targetDate);
            default:
                Console.Error.WriteLine($"Unknown --mode: {mode}. Valid: scheduled|catchup|manual|manual-redo|poc");
                return 5;
        }

        var crawlerArgs = new CrawlerArgs
        {
            Mode = crawlerMode,
            TargetDate = targetDate,
        };

        // ---- 建立 DI 容器 ----
        var services = new ServiceCollection();
        ConfigureServices(services);
        await using var sp = services.BuildServiceProvider();

        var orchestrator = sp.GetRequiredService<ICrawlerOrchestrator>();

        try
        {
            var run = await orchestrator.RunAsync(crawlerArgs);

            return run.Status switch
            {
                RunStatus.Success => 0,
                RunStatus.Skipped => 4,
                RunStatus.Failed  => DetermineFailureExitCode(run.ErrorMessage),
                _                 => 0,
            };
        }
        catch (CrawlNetworkException)
        {
            return 1;
        }
        catch (ParseException)
        {
            return 2;
        }
        catch (IOException)
        {
            return 3;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex}");
            return 1;
        }
    }

    private static int DetermineFailureExitCode(string? errorMessage)
    {
        if (errorMessage == null) return 1;
        if (errorMessage.Contains("IOException", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("No space left", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (errorMessage.Contains("ParseException", StringComparison.OrdinalIgnoreCase))
            return 2;
        return 1; // network failure default
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core
        services.AddSingleton<ITaiwanDateConverter, TaiwanDateConverter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IKeywordMatcher, KeywordMatcher>();

        // Storage
        services.AddSingleton<IDataPaths, LocalAppDataPaths>();
        services.AddSingleton<IAtomicJsonWriter, AtomicJsonWriter>();
        services.AddSingleton<ITenderRepository, TenderRepository>();
        services.AddSingleton<IDailySummaryRepository, DailySummaryRepository>();
        services.AddSingleton<ICrawlRunLogRepository, CrawlRunLogRepository>();
        services.AddSingleton<IErrorLogWriter, ErrorLogWriter>();
        services.AddSingleton<IKeywordsRepository, KeywordsRepository>();
        services.AddSingleton<IUserMarksRepository, UserMarksRepository>();
        services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();

        // Crawler：直接建立 HttpClient
        var httpHandler = new HttpClientHandler
        {
            // 不使用 AutomaticDecompression，避免在 Windows 某些環境下的問題
            // 若政府網站回傳 gzip，由 response.Content.ReadAsStringAsync 自動處理
            AllowAutoRedirect = false,  // 改由 WhitelistedRedirectHandler 自行處理
            UseCookies = true,
            CookieContainer = new CookieContainer(),
        };
        // 只有當 redirect Location 指向 web.pcc.gov.tw 才跟進，阻擋被導向他站
        var redirectHandler = new WhitelistedRedirectHandler(
            new[] { "web.pcc.gov.tw" },
            httpHandler);
        var httpClient = new HttpClient(redirectHandler)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        // 不設定 Accept/Accept-Language，使用 HttpClient 預設值
        // User-Agent 由 HttpClientCrawler 建構子設定
        services.AddSingleton(httpClient);
        services.AddSingleton<ICrawler>(sp =>
        {
            // 讀 AppSettings 取爬蟲調校（同步讀；首次執行可能不存在，用預設值）
            var settingsRepo = sp.GetRequiredService<IAppSettingsRepository>();
            AppSettings settings;
            try { settings = settingsRepo.LoadAsync().GetAwaiter().GetResult(); }
            catch { settings = new AppSettings(); }

            return new HttpClientCrawler(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IClock>(),
                requestDelayMs: settings.RequestDelayMs,
                maxRetries: settings.MaxRetries);
        });

        services.AddSingleton<ITenderParser, AngleSharpTenderParser>();
        services.AddSingleton<IDailySummaryService, DailySummaryService>();
        services.AddSingleton<ICrawlerOrchestrator, CrawlerOrchestrator>();
        services.AddSingleton<IProgressReporter, JsonLinesProgressReporter>();
    }

    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    // =====================================================================
    // Phase 2 PoC（保留供偵錯用）
    // =====================================================================
    private static async Task<int> RunPocAsync(DateOnly targetDate)
    {
        Console.WriteLine("=== Phase 2 PoC: 政府電子採購網可達性測試 ===");
        Console.WriteLine($"目標日期：{targetDate:yyyy-MM-dd}");

        // 直接 HTTP 測試（繞過 HttpClientCrawler）
        Console.WriteLine("\n--- 直接 HTTP 診斷 ---");
        try
        {
            using var diagHandler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
            using var diagRedirect = new WhitelistedRedirectHandler(
                new[] { "web.pcc.gov.tw" }, diagHandler);
            using var diagClient = new System.Net.Http.HttpClient(diagRedirect) { Timeout = TimeSpan.FromSeconds(10) };
            diagClient.DefaultRequestHeaders.Add("User-Agent", "TenderSearch/1.0");

            var dateStr = targetDate.ToString("yyyy/MM/dd");
            var diagUrl =
                "https://web.pcc.gov.tw/prkms/tender/common/basic/readTenderBasic" +
                "?pageSize=5&firstSearch=true&searchType=basic&isBinding=N&isLogIn=N" +
                "&tenderType=TENDER_DECLARATION&tenderWay=TENDER_WAY_1" +
                $"&dateType=isDate&tenderStartDate={Uri.EscapeDataString(dateStr)}" +
                $"&tenderEndDate={Uri.EscapeDataString(dateStr)}";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Console.Write("GET...");
            var diagResp = await diagClient.GetAsync(diagUrl);
            Console.WriteLine($" {(int)diagResp.StatusCode} ({sw.ElapsedMilliseconds}ms)");

            var diagHtml = await diagResp.Content.ReadAsStringAsync();
            Console.WriteLine($"Response length: {diagHtml.Length} chars");
            Console.WriteLine($"Contains tpam: {diagHtml.Contains("tpam")}");
            Console.WriteLine($"Contains pageCode2Img: {diagHtml.Contains("pageCode2Img")}");

            // 顯示「共有 N 筆資料」總筆數
            var totalMatch = System.Text.RegularExpressions.Regex.Match(
                diagHtml, @"共有<span class=""red"">\s*([0-9,]+)\s*</span>筆資料");
            if (totalMatch.Success)
                Console.WriteLine($"Total records on {dateStr}: {totalMatch.Groups[1].Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Diag error: {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine();

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
        };
        using var pocRedirectHandler = new WhitelistedRedirectHandler(
            new[] { "web.pcc.gov.tw" }, handler);
        using var httpClient = new HttpClient(pocRedirectHandler) { Timeout = TimeSpan.FromSeconds(30) };

        var crawler = new HttpClientCrawler(httpClient, new SystemClock());
        var parser = new AngleSharpTenderParser();

        try
        {
            var pages = await crawler.FetchAsync(
                targetDate,
                new[] { "公開招標" });

            Console.WriteLine($"抓取到 {pages.Count} 頁");

            foreach (var page in pages.Take(3))
            {
                if (page.Error != null)
                {
                    Console.WriteLine($"  頁 {page.PageNumber} 錯誤：{page.Error.Message}");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(page.Html))
                {
                    Console.WriteLine($"  頁 {page.PageNumber} HTML 為空");
                    continue;
                }

                try
                {
                    var items = parser.Parse(page.Html, DateTimeOffset.Now);
                    Console.WriteLine($"  頁 {page.PageNumber}：解析到 {items.Count} 筆標案");
                    foreach (var item in items.Take(3))
                        Console.WriteLine($"    [{item.SourcePk}] {item.AgencyName} - {item.TenderName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  頁 {page.PageNumber} 解析失敗：{ex.Message}");
                    Console.WriteLine($"  HTML 前 500 字：{page.Html[..Math.Min(500, page.Html.Length)]}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        Console.WriteLine("=== PoC 完成 ===");
        return 0;
    }
}
