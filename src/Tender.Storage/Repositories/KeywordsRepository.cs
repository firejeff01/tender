using System.Text;
using System.Text.Json;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface IKeywordsRepository
{
    Task<KeywordSet> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(KeywordSet set, CancellationToken ct = default);
}

public sealed class KeywordsRepository : IKeywordsRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public KeywordsRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<KeywordSet> LoadAsync(CancellationToken ct = default)
    {
        var filePath = _paths.KeywordsFile;

        if (!File.Exists(filePath))
        {
            // 首次執行，從內建資源建立預設檔案
            var defaultSet = GetDefaultKeywordSet();
            await SaveAsync(defaultSet, ct);
            return defaultSet;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
            return JsonSerializer.Deserialize<KeywordSet>(json, _readOptions)
                   ?? GetDefaultKeywordSet();
        }
        catch (JsonException)
        {
            return GetDefaultKeywordSet();
        }
    }

    public async Task SaveAsync(KeywordSet set, CancellationToken ct = default)
    {
        await _writer.WriteAsync(_paths.KeywordsFile, set, ct);
    }

    private static KeywordSet GetDefaultKeywordSet()
    {
        // 讀取嵌入資源
        var assembly = typeof(KeywordsRepository).Assembly;
        const string resourceName = "Tender.Storage.Resources.default-keywords.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new KeywordSet { Groups = Array.Empty<KeywordGroup>() };

        using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<KeywordSet>(json, _readOptions)
               ?? new KeywordSet { Groups = Array.Empty<KeywordGroup>() };
    }
}
