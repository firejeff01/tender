using Tender.Core.Models;

namespace Tender.Crawler.Parsing;

public interface ITenderParser
{
    /// <summary>
    /// 將單頁 HTML 解析為 TenderItem 集合。
    /// 解析失敗時拋 ParseException，呼叫端負責記錄到 errors.log 並繼續其他頁。
    /// </summary>
    IReadOnlyList<TenderItem> Parse(string html, DateTimeOffset now);
}

public sealed class ParseException : Exception
{
    public int PageNumber { get; }

    public ParseException(int pageNumber, string message, Exception? inner = null)
        : base(message, inner)
    {
        PageNumber = pageNumber;
    }
}
