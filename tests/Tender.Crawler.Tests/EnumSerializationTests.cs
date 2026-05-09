using System.Text.Json;
using FluentAssertions;
using Tender.Core.Models;

namespace Tender.Crawler.Tests;

/// <summary>
/// 驗證所有 Enum 序列化/反序列化為小寫（含 manual-redo 連字號）。
/// </summary>
public sealed class EnumSerializationTests
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ---- RunStatus ----

    [Theory]
    [InlineData(RunStatus.Success, "\"success\"")]
    [InlineData(RunStatus.Failed,  "\"failed\"")]
    [InlineData(RunStatus.Skipped, "\"skipped\"")]
    public void RunStatus_Serialize_ProducesLowercase(RunStatus status, string expected)
    {
        var json = JsonSerializer.Serialize(status);
        json.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"success\"", RunStatus.Success)]
    [InlineData("\"failed\"",  RunStatus.Failed)]
    [InlineData("\"skipped\"", RunStatus.Skipped)]
    public void RunStatus_Deserialize_FromLowercase(string json, RunStatus expected)
    {
        var result = JsonSerializer.Deserialize<RunStatus>(json);
        result.Should().Be(expected);
    }

    // ---- TriggerSource ----

    [Theory]
    [InlineData(TriggerSource.Scheduled,  "\"scheduled\"")]
    [InlineData(TriggerSource.Catchup,    "\"catchup\"")]
    [InlineData(TriggerSource.Manual,     "\"manual\"")]
    [InlineData(TriggerSource.ManualRedo, "\"manual-redo\"")]
    public void TriggerSource_Serialize_ProducesLowercase(TriggerSource source, string expected)
    {
        var json = JsonSerializer.Serialize(source);
        json.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"scheduled\"",   TriggerSource.Scheduled)]
    [InlineData("\"catchup\"",     TriggerSource.Catchup)]
    [InlineData("\"manual\"",      TriggerSource.Manual)]
    [InlineData("\"manual-redo\"", TriggerSource.ManualRedo)]
    public void TriggerSource_Deserialize_FromLowercase(string json, TriggerSource expected)
    {
        var result = JsonSerializer.Deserialize<TriggerSource>(json);
        result.Should().Be(expected);
    }

    // ---- ManualRedo 雙向（重點案例） ----

    [Fact]
    public void ManualRedo_RoundTrip_PreservesKebabCase()
    {
        var original = TriggerSource.ManualRedo;
        var json = JsonSerializer.Serialize(original);
        json.Should().Be("\"manual-redo\"", because: "SA data_models.md 明確要求 manual-redo 序列化為連字號格式");

        var deserialized = JsonSerializer.Deserialize<TriggerSource>(json);
        deserialized.Should().Be(original);
    }

    // ---- CrawlRun 整體 JSON 序列化 ----

    [Fact]
    public void CrawlRun_Json_ContainsLowercaseEnumValues()
    {
        var run = new CrawlRun
        {
            RunId = "20260508-170000",
            TargetDate = "2026-05-08",
            TriggerSource = TriggerSource.ManualRedo,
            StartedAt = new DateTimeOffset(2026, 5, 8, 17, 0, 0, TimeSpan.FromHours(8)),
            FinishedAt = new DateTimeOffset(2026, 5, 8, 17, 5, 0, TimeSpan.FromHours(8)),
            Status = RunStatus.Success,
        };

        var json = JsonSerializer.Serialize(run, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        json.Should().Contain("\"manual-redo\"", because: "triggerSource must be kebab-lower");
        json.Should().Contain("\"success\"", because: "status must be lowercase");
    }

    // ---- DailySummary 整體 JSON 反序列化 ----

    [Fact]
    public void DailySummary_Deserialize_FromSampleJson()
    {
        var json = """
            {
              "date": "2026-05-08",
              "totalCount": 55,
              "lastRunStatus": "success",
              "lastRunAt": "2026-05-08T17:05:30+08:00",
              "insertedCount": 55,
              "updatedCount": 0,
              "skippedCount": 0,
              "errorMessage": null
            }
            """;

        var summary = JsonSerializer.Deserialize<DailySummary>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        summary.Should().NotBeNull();
        summary!.LastRunStatus.Should().Be(RunStatus.Success);
        summary.TotalCount.Should().Be(55);
    }
}
