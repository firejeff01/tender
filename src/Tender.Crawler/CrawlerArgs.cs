using Tender.Core.Models;

namespace Tender.Crawler;

public sealed record CrawlerArgs
{
    public required CrawlerMode Mode { get; init; }
    public required DateOnly TargetDate { get; init; }
}
