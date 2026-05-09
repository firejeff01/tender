using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tender.Crawler.Reporting;

/// <summary>
/// 將進度事件以 JSON Lines 格式寫到 stdout。
/// 供桌面程式以 Process.StandardOutput 解析顯示進度條。
/// </summary>
public sealed class JsonLinesProgressReporter : IProgressReporter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Report(ProgressEvent evt)
    {
        var json = JsonSerializer.Serialize(evt, _options);
        Console.WriteLine(json);
    }
}
