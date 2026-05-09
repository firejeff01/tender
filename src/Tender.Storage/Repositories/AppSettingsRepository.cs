using System.Text;
using System.Text.Json;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface IAppSettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}

public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public AppSettingsRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        var filePath = _paths.AppSettingsFile;

        if (!File.Exists(filePath))
            return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
            return JsonSerializer.Deserialize<AppSettings>(json, _readOptions)
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        await _writer.WriteAsync(_paths.AppSettingsFile, settings, ct);
    }
}
