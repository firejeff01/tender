using System.Text.Json.Serialization;

namespace Tender.Core.Models;

/// <summary>使用者命名的搜尋條件組合。</summary>
public sealed record SavedSearch
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("keywordQuery")]
    public string? KeywordQuery { get; init; }

    [JsonPropertyName("activeKeywords")]
    public IReadOnlyList<string> ActiveKeywords { get; init; } = Array.Empty<string>();

    [JsonPropertyName("tenderMethod")]
    public string? TenderMethod { get; init; }

    [JsonPropertyName("procurementType")]
    public string? ProcurementType { get; init; }

    [JsonPropertyName("budgetMin")]
    public long? BudgetMin { get; init; }

    [JsonPropertyName("budgetMax")]
    public long? BudgetMax { get; init; }

    [JsonPropertyName("showActiveOnly")]
    public bool ShowActiveOnly { get; init; }

    [JsonPropertyName("showFavoritesOnly")]
    public bool ShowFavoritesOnly { get; init; }
}

public sealed record SavedSearchSet
{
    [JsonPropertyName("searches")]
    public IReadOnlyList<SavedSearch> Searches { get; init; } = Array.Empty<SavedSearch>();
}
