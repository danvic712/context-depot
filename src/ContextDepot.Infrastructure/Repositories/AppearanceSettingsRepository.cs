using System.Text.Json;
using System.Text.Json.Serialization;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Settings.Enums;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class AppearanceSettingsRepository(
    ContextDepotDbContext db,
    TimeProvider timeProvider,
    DatabaseApplicationSettingsSnapshotLoader snapshotLoader) : IAppearanceSettingsRepository
{
    private const string ThemeKey = "ContextDepot:Appearance:Theme";
    private const string LanguageKey = "ContextDepot:Appearance:Language";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public async Task<AppearanceSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var records = await db.ApplicationSettings.AsNoTracking()
            .Where(setting => setting.Key == ThemeKey || setting.Key == LanguageKey)
            .ToDictionaryAsync(setting => setting.Key, setting => setting.ValueJson, cancellationToken);
        return new AppearanceSettingsDto(Read<Theme>(records, ThemeKey), Read<Language>(records, LanguageKey));
    }

    public Task SetThemeAsync(Theme theme, CancellationToken cancellationToken) =>
        UpdateAsync(ThemeKey, theme, cancellationToken);

    public Task SetLanguageAsync(Language language, CancellationToken cancellationToken) =>
        UpdateAsync(LanguageKey, language, cancellationToken);

    private async Task UpdateAsync<T>(string key, T value, CancellationToken cancellationToken) where T : struct, Enum
    {
        var valueJson = JsonSerializer.Serialize(value, JsonOptions);
        var now = timeProvider.GetUtcNow();
        var affected = await db.ApplicationSettings.Where(setting => setting.Key == key)
            .ExecuteUpdateAsync(update => update
                .SetProperty(setting => setting.ValueJson, valueJson)
                .SetProperty(setting => setting.UpdatedAt, now), cancellationToken);
        if (affected != 1)
        {
            throw new InvalidOperationException("Required appearance setting is missing.");
        }

        await snapshotLoader.RefreshAsync(cancellationToken);
    }

    private static T Read<T>(IReadOnlyDictionary<string, string> records, string key) where T : struct, Enum =>
        records.TryGetValue(key, out var value)
            ? JsonSerializer.Deserialize<T>(value, JsonOptions)
            : throw new InvalidOperationException("Required appearance setting is missing.");
}
