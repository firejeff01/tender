using FluentAssertions;
using System.Text.Json;
using Tender.Storage.Atomic;
using Xunit;

namespace Tender.Storage.Tests;

public sealed class AtomicJsonWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AtomicJsonWriter _sut;

    public AtomicJsonWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tender-tests-atomic", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new AtomicJsonWriter();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task WriteAsync_NormalWrite_ProducesCorrectContent()
    {
        var finalPath = Path.Combine(_tempDir, "test.json");
        var data = new { Name = "測試", Value = 42 };

        await _sut.WriteAsync(finalPath, data);

        File.Exists(finalPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(finalPath);
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("Name").GetString().Should().Be("測試");
        parsed.RootElement.GetProperty("Value").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task WriteAsync_TmpFileRemovedOnSuccess()
    {
        var finalPath = Path.Combine(_tempDir, "clean.json");
        await _sut.WriteAsync(finalPath, new { X = 1 });

        var tmpPath = finalPath + ".tmp";
        File.Exists(tmpPath).Should().BeFalse("tmp file should be moved to final");
        File.Exists(finalPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectoryIfNotExists()
    {
        var nested = Path.Combine(_tempDir, "a", "b", "c");
        var finalPath = Path.Combine(nested, "test.json");

        await _sut.WriteAsync(finalPath, new { Nested = true });

        File.Exists(finalPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAsync_OverwritesExistingFile()
    {
        var finalPath = Path.Combine(_tempDir, "overwrite.json");

        await _sut.WriteAsync(finalPath, new { V = 1 });
        await _sut.WriteAsync(finalPath, new { V = 2 });

        var json = await File.ReadAllTextAsync(finalPath);
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("V").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task WriteAsync_ConcurrentWrites_AtLeastOneSucceedsWithValidJson()
    {
        // 說明：AtomicJsonWriter 不支援同一檔案的並發寫入（.tmp 互斥）。
        // 並發控制應由上層 crawler.lock 機制處理。
        // 本測試驗證：兩個寫入至少有一個成功，且成功的那個產出合法 JSON。
        var finalPath = Path.Combine(_tempDir, "concurrent.json");

        var t1 = _sut.WriteAsync(finalPath, new { Writer = "first", Items = Enumerable.Range(1, 100).ToList() });
        var t2 = _sut.WriteAsync(finalPath, new { Writer = "second", Items = Enumerable.Range(1, 100).ToList() });

        // 至少一個應成功，允許其中一個拋出 IOException（.tmp 衝突）
        Exception? ex1 = null;
        Exception? ex2 = null;
        try { await t1; } catch (IOException e) { ex1 = e; }
        try { await t2; } catch (IOException e) { ex2 = e; }

        // 不應兩個都失敗
        (ex1 is null || ex2 is null).Should().BeTrue("at least one concurrent write should succeed");

        // 成功的寫入應產出合法 JSON
        if (File.Exists(finalPath))
        {
            var json = await File.ReadAllTextAsync(finalPath);
            var act = () => JsonDocument.Parse(json);
            act.Should().NotThrow("the successful write should produce valid JSON");
        }
    }
}
