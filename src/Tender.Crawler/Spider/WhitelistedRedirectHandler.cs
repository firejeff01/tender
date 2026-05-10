using System.Net;

namespace Tender.Crawler.Spider;

/// <summary>
/// 自行處理 HTTP 3xx redirect。僅當 Location header 指向白名單 host 時才跟進，
/// 阻擋上游回應把流量導向非預期主機（防禦深度）。
/// 用法：搭配 <c>HttpClientHandler.AllowAutoRedirect=false</c> 包在外層。
/// </summary>
public sealed class WhitelistedRedirectHandler : DelegatingHandler
{
    private const int MaxRedirects = 5;
    private readonly HashSet<string> _allowedHosts;

    public WhitelistedRedirectHandler(
        IEnumerable<string> allowedHosts,
        HttpMessageHandler innerHandler) : base(innerHandler)
    {
        _allowedHosts = new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);
        var redirectCount = 0;

        while (IsRedirect(response.StatusCode) && redirectCount < MaxRedirects)
        {
            var location = response.Headers.Location;
            if (location is null) break;

            var target = location.IsAbsoluteUri
                ? location
                : new Uri(request.RequestUri!, location);

            // 非白名單 host：停止跟進，把 3xx response 回傳給呼叫端
            if (!_allowedHosts.Contains(target.Host)) break;

            response.Dispose();

            // 303 See Other 一律改 GET（HTTP 規範）；其他保留原方法
            var method = response.StatusCode == HttpStatusCode.SeeOther
                ? HttpMethod.Get
                : request.Method;

            var nextRequest = new HttpRequestMessage(method, target);
            foreach (var header in request.Headers)
                nextRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

            response = await base.SendAsync(nextRequest, ct);
            request = nextRequest;
            redirectCount++;
        }

        return response;
    }

    private static bool IsRedirect(HttpStatusCode code) =>
        code == HttpStatusCode.MovedPermanently     // 301
        || code == HttpStatusCode.Found              // 302
        || code == HttpStatusCode.SeeOther           // 303
        || code == HttpStatusCode.TemporaryRedirect  // 307
        || code == HttpStatusCode.PermanentRedirect; // 308
}
