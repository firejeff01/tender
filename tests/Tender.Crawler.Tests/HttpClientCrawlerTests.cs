using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Tender.Core.Clock;
using Tender.Core.Exceptions;
using Tender.Crawler.Spider;

namespace Tender.Crawler.Tests;

/// <summary>
/// HttpClientCrawler 單元測試，用 Mock&lt;HttpMessageHandler&gt; 模擬回應。
/// 不進行真實 HTTP 請求。
/// </summary>
public sealed class HttpClientCrawlerTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clockMock = new();

    private HttpClientCrawler CreateCrawler(int requestDelayMs = 0, int maxRetries = 3)
    {
        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = null,
        };

        return new HttpClientCrawler(httpClient, _clockMock.Object,
            requestDelayMs: requestDelayMs,
            maxRetries: maxRetries);
    }

    private static string MakeEmptyTenderHtml() =>
        "<html><body><table id=\"tpam\"><thead><tr><th>序號</th></tr></thead><tbody></tbody></table></body></html>";

    private void SetupHandler(HttpStatusCode status, string content, int times = 1)
    {
        _handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/html"),
            });

        for (int i = 1; i < times; i++)
        {
            _handlerMock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }
    }

    private void SetupHandlerSequence(params (HttpStatusCode status, string content)[] responses)
    {
        var setup = _handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        foreach (var (status, content) in responses)
        {
            setup = setup.ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/html"),
            });
        }
    }

    // ---- 正常請求 ----

    [Fact]
    public async Task FetchAsync_Success_ReturnsPageWithHtml()
    {
        var targetDate = new DateOnly(2026, 5, 9);
        var html = MakeEmptyTenderHtml();

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0);
        var pages = await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        pages.Should().HaveCount(1);
        pages[0].Html.Should().NotBeNullOrEmpty();
        pages[0].Error.Should().BeNull();
        pages[0].PageNumber.Should().Be(1);
    }

    // ---- 請求 URL 驗證 ----

    [Fact]
    public async Task FetchAsync_RequestUrl_ContainsCorrectEndpoint()
    {
        var targetDate = new DateOnly(2026, 5, 9);
        HttpRequestMessage? capturedRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MakeEmptyTenderHtml(), Encoding.UTF8, "text/html"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0);
        await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsoluteUri.Should().Contain("readTenderBasic");
    }

    // ---- 表單欄位驗證 ----

    [Fact]
    public async Task FetchAsync_RequestUrl_ContainsCorrectQueryParameters()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        HttpRequestMessage? capturedRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MakeEmptyTenderHtml(), Encoding.UTF8, "text/html"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0);
        await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        var query = capturedRequest.RequestUri!.Query;
        query.Should().Contain("tenderWay=TENDER_WAY_1");
        query.Should().Contain("tenderType=TENDER_DECLARATION");
        query.Should().Contain("dateType=isDate");
        query.Should().Contain("tenderStartDate=2026%2F05%2F08");
        query.Should().Contain("tenderEndDate=2026%2F05%2F08");
    }

    // ---- User-Agent 驗證 ----

    [Fact]
    public async Task FetchAsync_Request_ContainsTenderSearchUserAgent()
    {
        var targetDate = new DateOnly(2026, 5, 9);
        HttpRequestMessage? capturedRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MakeEmptyTenderHtml(), Encoding.UTF8, "text/html"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0);
        await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        capturedRequest!.Headers.TryGetValues("User-Agent", out var userAgentValues);
        var userAgent = string.Join(" ", userAgentValues ?? Array.Empty<string>());
        userAgent.Should().Contain("TenderSearch", because: "User-Agent must identify as TenderSearch");
    }

    // ---- 5xx 重試 ----

    [Fact]
    public async Task FetchAsync_ServerError_RetriesUpToMaxRetries()
    {
        var targetDate = new DateOnly(2026, 5, 9);
        int callCount = 0;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((_, _) => callCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("error"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0, maxRetries: 2);
        var pages = await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        // 1 次原始 + 2 次重試 = 3 次
        callCount.Should().Be(3);

        // 仍然返回一頁（含 error）
        pages.Should().HaveCount(1);
        pages[0].Error.Should().NotBeNull();
    }

    // ---- 4xx 不重試 ----

    [Fact]
    public async Task FetchAsync_ClientError_DoesNotRetry()
    {
        var targetDate = new DateOnly(2026, 5, 9);
        int callCount = 0;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((_, _) => callCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("forbidden"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0, maxRetries: 3);
        var pages = await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        // 4xx 不重試，只呼叫 1 次
        callCount.Should().Be(1);
        pages[0].Error.Should().NotBeNull();
    }

    // ---- 多招標方式 ----

    [Fact]
    public async Task FetchAsync_MultipleTenderMethods_SendsRequestForEach()
    {
        var targetDate = new DateOnly(2026, 5, 9);
        int callCount = 0;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((_, _) => callCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MakeEmptyTenderHtml(), Encoding.UTF8, "text/html"),
            });

        var methods = new[] { "公開招標", "公開取得報價單或企劃書", "經公開評選或公開徵求之限制性招標" };
        var crawler = CreateCrawler(requestDelayMs: 0);
        var pages = await crawler.FetchAsync(targetDate, methods);

        // 每種招標方式各至少 1 次請求
        callCount.Should().BeGreaterThanOrEqualTo(3);
        pages.Should().HaveCount(3);
    }

    // ---- HttpRequestException → CrawlNetworkException ----

    [Fact]
    public async Task FetchAsync_NetworkException_ThrowsCrawlNetworkException()
    {
        var targetDate = new DateOnly(2026, 5, 9);

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var crawler = CreateCrawler(requestDelayMs: 0, maxRetries: 0);
        var pages = await crawler.FetchAsync(targetDate, new[] { "公開招標" });

        // 應以 CrawlNetworkException 記錄在 page.Error
        pages.Should().HaveCount(1);
        pages[0].Error.Should().BeOfType<CrawlNetworkException>();
    }

    // ---- 未知招標方式忽略 ----

    [Fact]
    public async Task FetchAsync_UnknownTenderMethod_SkipsWithoutThrow()
    {
        var targetDate = new DateOnly(2026, 5, 9);

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MakeEmptyTenderHtml(), Encoding.UTF8, "text/html"),
            });

        var crawler = CreateCrawler(requestDelayMs: 0);
        // 一個有效，一個無效
        var pages = await crawler.FetchAsync(targetDate, new[] { "公開招標", "未知招標方式" });

        // 僅有效的那一個產生請求
        pages.Should().HaveCount(1);
    }
}
