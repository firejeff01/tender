using System.Text;
using System.Text.Json;

namespace Tender.Storage.Atomic;

/// <summary>
/// 原子寫入：先寫暫存檔 .tmp，再 File.Move 覆蓋目標。
/// 若寫入中斷，目標檔案不受影響（.tmp 殘留可被清理）。
/// </summary>
public sealed class AtomicJsonWriter : IAtomicJsonWriter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task WriteAsync<T>(string finalPath, T data, CancellationToken ct = default)
    {
        var tmpPath = finalPath + ".tmp";

        // 確保目錄存在
        var dir = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // 寫入暫存檔
        var json = JsonSerializer.Serialize(data, _options);
        await File.WriteAllTextAsync(tmpPath, json, Encoding.UTF8, ct);

        // 原子替換（.NET 6+ 在 NTFS 上為原子操作）
        File.Move(tmpPath, finalPath, overwrite: true);
    }
}
