namespace Tender.Core.Exceptions;

/// <summary>網路或外部資源不可用。</summary>
public class CrawlNetworkException : Exception
{
    public CrawlNetworkException() { }
    public CrawlNetworkException(string message) : base(message) { }
    public CrawlNetworkException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>JSON 檔案損毀無法解析。</summary>
public class CorruptedDataException : Exception
{
    public string FilePath { get; }

    public CorruptedDataException(string filePath) : base($"Data file is corrupted: {filePath}")
    {
        FilePath = filePath;
    }

    public CorruptedDataException(string filePath, string message) : base(message)
    {
        FilePath = filePath;
    }

    public CorruptedDataException(string filePath, string message, Exception inner)
        : base(message, inner)
    {
        FilePath = filePath;
    }
}

/// <summary>另一個 run 正在進行中（已持有 crawler.lock）。</summary>
public class CrawlerLockedException : Exception
{
    public CrawlerLockedException() : base("Another crawler run is already in progress.") { }
    public CrawlerLockedException(string message) : base(message) { }
    public CrawlerLockedException(string message, Exception inner) : base(message, inner) { }
}
