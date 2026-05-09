using Tender.Core.Models;

namespace Tender.Core.Keywords;

/// <summary>
/// 關鍵字命中服務實作。
/// 採子字串包含（模糊查詢），大小寫不敏感（對中文無效，但對英文關鍵字如 AI、AR、VR 有作用）。
/// targetField 規則：
///   - "tenderName"  → 只比對 TenderItem.TenderName
///   - "agencyName"  → 只比對 TenderItem.AgencyName
///   - "any"         → 兩者都比對，任一命中即視為命中
/// </summary>
public sealed class KeywordMatcher : IKeywordMatcher
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Match(TenderItem item, KeywordSet keywordSet)
    {
        var matched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in keywordSet.Groups)
        {
            foreach (var kwItem in group.Items)
            {
                if (!kwItem.Enabled)
                    continue;

                if (IsMatch(item, kwItem.Keyword, kwItem.TargetField))
                    matched.Add(kwItem.Keyword);
            }
        }

        return matched.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public bool IsMatch(TenderItem item, string keyword, string targetField)
    {
        return targetField switch
        {
            "tenderName" => ContainsKeyword(item.TenderName, keyword),
            "agencyName" => ContainsKeyword(item.AgencyName, keyword),
            "any" => ContainsKeyword(item.TenderName, keyword) || ContainsKeyword(item.AgencyName, keyword),
            _ => ContainsKeyword(item.TenderName, keyword) || ContainsKeyword(item.AgencyName, keyword),
        };
    }

    private static bool ContainsKeyword(string? text, string keyword)
    {
        if (text is null) return false;
        return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
