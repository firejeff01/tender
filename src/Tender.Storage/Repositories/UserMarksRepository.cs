using System.Text;
using System.Text.Json;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface IUserMarksRepository
{
    Task<UserMarks> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(UserMarks marks, CancellationToken ct = default);
}

public sealed class UserMarksRepository : IUserMarksRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public UserMarksRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<UserMarks> LoadAsync(CancellationToken ct = default)
    {
        var filePath = _paths.UserMarksFile;

        if (!File.Exists(filePath))
            return new UserMarks { Marks = Array.Empty<UserMark>() };

        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
            return JsonSerializer.Deserialize<UserMarks>(json, _readOptions)
                   ?? new UserMarks { Marks = Array.Empty<UserMark>() };
        }
        catch (JsonException)
        {
            return new UserMarks { Marks = Array.Empty<UserMark>() };
        }
    }

    public async Task SaveAsync(UserMarks marks, CancellationToken ct = default)
    {
        await _writer.WriteAsync(_paths.UserMarksFile, marks, ct);
    }
}
