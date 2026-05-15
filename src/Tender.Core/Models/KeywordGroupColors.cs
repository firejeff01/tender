namespace Tender.Core.Models;

/// <summary>
/// 關鍵字群組強調色預設色盤（暖色系，依出現順序循環）。
/// 同時用於：(1) keywords.json 缺 Color 時 fallback、(2) 管理 UI 的 preset palette、
/// (3) 新增群組時指派下一個未用色。
/// </summary>
public static class KeywordGroupColors
{
    public static readonly IReadOnlyList<string> Palette = new[]
    {
        "#8B6F47", // 暖棕
        "#9C5A8C", // 紫
        "#A0524D", // 磚紅
        "#7A8B5C", // 鼠尾草綠
        "#C4823C", // 橘
        "#5B7C8C", // 灰藍
        "#8B5A3C", // 深棕
        "#6B8E6B", // 葉綠
        "#A5664E", // 鏽紅
    };

    /// <summary>依索引取色（超出範圍會循環）。</summary>
    public static string GetByIndex(int index) => Palette[((index % Palette.Count) + Palette.Count) % Palette.Count];
}
