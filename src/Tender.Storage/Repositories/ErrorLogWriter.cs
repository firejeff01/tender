using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface IErrorLogWriter
{
    /// <summary>
    /// 對指定日的 errors.log 追加一行 JSON Lines 紀錄。
    /// </summary>
    Task AppendAsync(DateOnly date, ErrorLogEntry entry, CancellationToken ct = default);
}

public sealed record ErrorLogEntry(
    DateTimeOffset Timestamp,
    string Severity,        // "info" | "warning" | "error"
    string Source,          // "crawler" | "desktop" | "storage"
    string? RunId,
    string Message,
    string? ExceptionDetail,
    int? Page);

public sealed class ErrorLogWriter : IErrorLogWriter
{
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDataPaths _paths;

    public ErrorLogWriter(IDataPaths paths)
    {
        _paths = paths;
    }

    public async Task AppendAsync(DateOnly date, ErrorLogEntry entry, CancellationToken ct = default)
    {
        var filePath = _paths.ErrorsLogFile(date);

        // 確保目錄存在
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var line = JsonSerializer.Serialize(entry, _writeOptions) + Environment.NewLine;

        // Append-only：FileShare.Read 允許讀者同時讀取
        await using var fs = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);

        var bytes = Encoding.UTF8.GetBytes(line);
        await fs.WriteAsync(bytes, ct);
    }
}
