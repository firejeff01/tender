using System.Text.Json.Serialization;

namespace Tender.Core.Models;

public sealed record UserMarks
{
    [JsonPropertyName("marks")]
    public required IReadOnlyList<UserMark> Marks { get; init; }
}

public sealed record UserMark
{
    [JsonPropertyName("sourcePk")]
    public required string SourcePk { get; init; }

    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; init; }

    [JsonPropertyName("isRead")]
    public bool IsRead { get; init; }

    [JsonPropertyName("isExcluded")]
    public bool IsExcluded { get; init; }

    [JsonPropertyName("note")]
    public string Note { get; init; } = string.Empty;
}
