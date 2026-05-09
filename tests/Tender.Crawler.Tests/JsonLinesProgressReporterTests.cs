using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tender.Crawler.Reporting;

namespace Tender.Crawler.Tests;

/// <summary>
/// JsonLinesProgressReporter 測試：驗證 JSON Lines 輸出格式正確。
/// </summary>
public sealed class JsonLinesProgressReporterTests
{
    [Fact]
    public void Report_WritesJsonLineToConsole()
    {
        // 重導 Console.Out 以捕捉輸出
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        var original = Console.Out;
        Console.SetOut(writer);

        try
        {
            var reporter = new JsonLinesProgressReporter();
            reporter.Report(new ProgressEvent("fetching", "Test message", 1, 50.0));

            var line = sb.ToString().Trim();
            line.Should().NotBeEmpty();

            // 驗證是合法 JSON
            var json = JsonDocument.Parse(line);
            json.RootElement.GetProperty("stage").GetString().Should().Be("fetching");
            json.RootElement.GetProperty("message").GetString().Should().Be("Test message");
            json.RootElement.GetProperty("pageNumber").GetInt32().Should().Be(1);
            json.RootElement.GetProperty("percentComplete").GetDouble().Should().Be(50.0);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Report_NullOptionalFields_OmittedFromJson()
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        var original = Console.Out;
        Console.SetOut(writer);

        try
        {
            var reporter = new JsonLinesProgressReporter();
            reporter.Report(new ProgressEvent("start", "Starting", null, null));

            var line = sb.ToString().Trim();
            var json = JsonDocument.Parse(line);

            // null 欄位不應出現（DefaultIgnoreCondition.WhenWritingNull）
            json.RootElement.TryGetProperty("pageNumber", out _).Should().BeFalse();
            json.RootElement.TryGetProperty("percentComplete", out _).Should().BeFalse();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Report_MultipleEvents_EachOnSeparateLine()
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        var original = Console.Out;
        Console.SetOut(writer);

        try
        {
            var reporter = new JsonLinesProgressReporter();
            reporter.Report(new ProgressEvent("start", "Start", null, 0));
            reporter.Report(new ProgressEvent("done", "Done", null, 100));

            var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().HaveCount(2);
            foreach (var line in lines)
                JsonDocument.Parse(line.Trim()); // 每行都是合法 JSON
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
