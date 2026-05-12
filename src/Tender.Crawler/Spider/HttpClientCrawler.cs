using System.Net;
using System.Text.RegularExpressions;
using Tender.Core.Clock;
using Tender.Core.Constants;
using Tender.Core.Exceptions;

namespace Tender.Crawler.Spider;

/// <summary>
/// 用 HttpClient GET 對政府電子採購網抓取標案列表。
/// - 端點以 query string 帶查詢條件（GET，不是 POST）
/// - 日期格式為西元 yyyy/MM/dd（不是民國），dateType=isDate + 起迄同日 = 鎖單日
/// - 分頁：每頁 pageSize=100，依 tbody 中 &lt;tr&gt; 數量判斷是否還有下一頁
/// - 重試：手動重試迴圈，指數退避 1s → 2s → 4s，最多 3 次
/// - 禮貌延遲：每頁請求間 1.5 秒
/// - User-Agent：TenderSearch/1.0（PoC 確認簡單 UA 比 Chrome UA 還快）
/// </summary>
public sealed class HttpClientCrawler : ICrawler
{
    // 使用 basic 端點與政府電子採購網 UI 一致：advanced 會少列「未來日公告日的更正公告」，
    // basic 則完整列出當日查詢條件下網站所顯示的所有筆數。
    private const string BaseUrl = "https://web.pcc.gov.tw/prkms/tender/common/basic/readTenderBasic";
    // 使用簡單 UA 避免觸發政府網站防爬機制
    // PoC 驗證：複雜的 Chrome UA 會導致伺服器等待，簡單 UA 可在 < 1 秒內得到回應
    private const string UserAgentString = "TenderSearch/1.0";

    private readonly HttpClient _httpClient;
    private readonly IClock _clock;
    private readonly int _requestDelayMs;
    private readonly int _maxRetries;

    public HttpClientCrawler(
        HttpClient httpClient,
        IClock clock,
        int requestDelayMs = 1500,
        int maxRetries = 3)
    {
        _httpClient = httpClient;
        _clock = clock;
        _requestDelayMs = requestDelayMs;
        _maxRetries = maxRetries;

        // 設定 User-Agent（若尚未設定）
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgentString);
    }

    public async Task<IReadOnlyList<FetchedPage>> FetchAsync(
        DateOnly targetDate,
        IReadOnlyList<string> tenderMethods,
        CancellationToken ct = default)
    {
        var pages = new List<FetchedPage>();

        foreach (var method in tenderMethods)
        {
            if (!TenderMethodMapping.BusinessNameToOptionValue.TryGetValue(method, out var optionValue))
            {
                Console.Error.WriteLine($"[Crawler] Unknown tender method: {method}, skipping.");
                continue;
            }

            var methodPages = await FetchMethodAsync(targetDate, method, optionValue, ct);
            pages.AddRange(methodPages);
        }

        return pages.AsReadOnly();
    }

    private async Task<IReadOnlyList<FetchedPage>> FetchMethodAsync(
        DateOnly targetDate,
        string methodName,
        string optionValue,
        CancellationToken ct)
    {
        var pages = new List<FetchedPage>();
        int pageNumber = 1;
        bool hasMore = true;

        while (hasMore && !ct.IsCancellationRequested)
        {
            if (pageNumber > 1)
            {
                await Task.Delay(_requestDelayMs, ct);
            }

            var (page, morePages) = await FetchPageAsync(
                targetDate, methodName, optionValue, pageNumber, ct);

            pages.Add(page);
            hasMore = morePages && page.Error == null;
            pageNumber++;
        }

        return pages;
    }

    // 政府網站雖然 UI 下拉只有 10/20/50/100，但網址列 pageSize 可拉大，
    // 一次拉回所有資料；超過實際筆數會直接回全部、不會報錯。
    // PoC 觀察：5/8 公開招標 674 筆，pageSize=2000 一次 ~1 秒拿完，比 7 頁分頁快約 13 秒。
    // 保留分頁邏輯當安全網：萬一某日某方式破 2000 筆，仍會自動抓下一頁。
    private const int PageSize = 2000;

    // displaytag 分頁參數：d-XXXXX-p=N 控制頁碼，XXXXX 為網頁內表格 ID。
    // 政府電子採購網的招標查詢結果表格 ID 為 49738。
    // pageIndex 參數對 gov 無效，必須用 displaytag 約定。
    private const string PaginationParam = "d-49738-p";

    private async Task<(FetchedPage page, bool hasMore)> FetchPageAsync(
        DateOnly targetDate,
        string methodName,
        string optionValue,
        int pageNumber,
        CancellationToken ct)
    {
        // 政府網站要求 GET、西元日期 yyyy/MM/dd、dateType=isDate + 起迄同日 = 鎖單日。
        // PoC 確認：dateType=isSpdt 是「等標期內」（會帶到未來日期），不是當日；
        //          dateType=isDate 才是「公告日期區間」。
        var dateStr = targetDate.ToString("yyyy/MM/dd");
        var query = new Dictionary<string, string>
        {
            ["pageSize"]        = PageSize.ToString(),
            ["firstSearch"]     = "true",
            ["searchType"]      = "basic",
            ["isBinding"]       = "N",
            ["isLogIn"]         = "N",
            ["tenderType"]      = "TENDER_DECLARATION",
            ["tenderWay"]       = optionValue,
            ["dateType"]        = "isDate",
            ["tenderStartDate"] = dateStr,
            ["tenderEndDate"]   = dateStr,
        };
        if (pageNumber > 1)
            query[PaginationParam] = pageNumber.ToString();

        var url = BuildUrl(BaseUrl, query);

        try
        {
            HttpResponseMessage response;
            try
            {
                response = await ExecuteWithRetryAsync(async () =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Referrer = new Uri(BaseUrl);
                    return await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                }, pageNumber, methodName, ct);
            }
            catch (CrawlNetworkException)
            {
                throw;
            }

            if ((int)response.StatusCode >= 400)
            {
                var errMsg = $"HTTP {(int)response.StatusCode} on page {pageNumber} ({methodName})";
                return (new FetchedPage(pageNumber, url, null, new CrawlNetworkException(errMsg)), false);
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var hasMore = HasMorePages(html, pageNumber);

            return (new FetchedPage(pageNumber, url, html, null), hasMore);
        }
        catch (CrawlNetworkException ex)
        {
            return (new FetchedPage(pageNumber, url, null, ex), false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (new FetchedPage(pageNumber, url, null, new CrawlNetworkException($"Unexpected error: {ex.Message}", ex)), false);
        }
    }

    private static string BuildUrl(string baseUrl, IDictionary<string, string> query)
    {
        var sb = new System.Text.StringBuilder(baseUrl);
        sb.Append('?');
        bool first = true;
        foreach (var kv in query)
        {
            if (!first) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kv.Value));
            first = false;
        }
        return sb.ToString();
    }

    /// <summary>
    /// 手動重試邏輯：HttpRequestException 或 timeout 時重試，4xx / 使用者取消 不重試。
    /// </summary>
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> action,
        int pageNumber,
        string methodName,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                Console.Error.WriteLine($"[Crawler] Retry {attempt}/{_maxRetries} in {delay.TotalSeconds}s for page {pageNumber} ({methodName})");
                await Task.Delay(delay, ct);
            }

            try
            {
                var resp = await action();

                // 5xx → 重試
                if ((int)resp.StatusCode >= 500)
                {
                    lastException = new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                    continue;
                }

                // 4xx 不重試，直接回傳
                return resp;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 使用者主動取消，直接傳播
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // HttpClient timeout（非使用者取消），視為網路錯誤，可重試
                lastException = new CrawlNetworkException($"Timeout on page {pageNumber} ({methodName}): {ex.Message}", ex);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
        }

        throw new CrawlNetworkException(
            $"Failed after {_maxRetries} retries on page {pageNumber} ({methodName}): {lastException?.Message}",
            lastException ?? new HttpRequestException("Unknown crawler error"));
    }

    // 解析回應裡「共有 <span class="red"> N </span>筆資料」並比對目前頁碼，
    // 決定是否還有下一頁。比起數 tbody 的 <tr> 元素，這個總筆數方式更可靠。
    private static readonly Regex TotalCountRegex =
        new(@"共有<span class=""red"">\s*([0-9,]+)\s*</span>筆資料",
            RegexOptions.Compiled);

    private static bool HasMorePages(string html, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        var match = TotalCountRegex.Match(html);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups[1].Value.Replace(",", ""), out var total))
            return false;

        var totalPages = (total + PageSize - 1) / PageSize;
        return pageNumber < totalPages;
    }
}
