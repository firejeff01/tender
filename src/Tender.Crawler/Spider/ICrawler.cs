namespace Tender.Crawler.Spider;

public interface ICrawler
{
    /// <summary>
    /// 對政府電子採購網查詢指定日期、指定招標方式的標案，回傳原始 HTML 頁面集合。
    /// 內部負責分頁、重試、禮貌延遲、User-Agent 設定。
    /// </summary>
    Task<IReadOnlyList<FetchedPage>> FetchAsync(
        DateOnly targetDate,
        IReadOnlyList<string> tenderMethods,
        CancellationToken ct = default);
}

public sealed record FetchedPage(int PageNumber, string SourceUrl, string? Html, Exception? Error);
