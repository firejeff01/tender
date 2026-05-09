namespace Tender.Storage.Atomic;

/// <summary>
/// 暫存檔加原子替換策略的共用實作。
/// 寫入流程：寫 .tmp → fsync → File.Move(overwrite: true)。
/// </summary>
public interface IAtomicJsonWriter
{
    Task WriteAsync<T>(string finalPath, T data, CancellationToken ct = default);
}
