using System.Text;
using System.Text.Json;
using Tender.Core.Constants;
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
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _readOptions)
                           ?? new AppSettings();
            return MigrateLegacyTenderMethods(settings);
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

    /// <summary>
    /// 將既有 settings.json 中已重新命名的招標方式字串換成目前名稱，
    /// 避免重新命名後使用者勾選狀態被視為「沒勾」。
    /// </summary>
    private static AppSettings MigrateLegacyTenderMethods(AppSettings settings)
    {
        var migrated = settings.TargetTenderMethods
            .Select(TenderMethodMapping.MigrateLegacyName)
            .Distinct()
            .ToList();

        if (migrated.SequenceEqual(settings.TargetTenderMethods))
            return settings;

        return settings with { TargetTenderMethods = migrated.AsReadOnly() };
    }
}
