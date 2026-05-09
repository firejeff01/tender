using System.Text;
using System.Text.Json;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface ISavedSearchesRepository
{
    Task<SavedSearchSet> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(SavedSearchSet set, CancellationToken ct = default);
}

public sealed class SavedSearchesRepository : ISavedSearchesRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public SavedSearchesRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<SavedSearchSet> LoadAsync(CancellationToken ct = default)
    {
        var filePath = _paths.SavedSearchesFile;
        if (!File.Exists(filePath))
            return new SavedSearchSet();

        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
            return JsonSerializer.Deserialize<SavedSearchSet>(json, _readOptions) ?? new SavedSearchSet();
        }
        catch (JsonException)
        {
            return new SavedSearchSet();
        }
    }

    public Task SaveAsync(SavedSearchSet set, CancellationToken ct = default)
        => _writer.WriteAsync(_paths.SavedSearchesFile, set, ct);
}
