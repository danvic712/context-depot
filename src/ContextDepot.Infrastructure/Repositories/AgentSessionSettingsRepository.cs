using System.Text.Json;
using ContextDepot.Infrastructure.Exceptions;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class AgentSessionSettingsRepository(
    ContextDepotDbContext db,
    TimeProvider timeProvider,
    DatabaseApplicationSettingsSnapshotLoader snapshotLoader) : IAgentSessionSettingsRepository
{
    private const string EnabledKey = "AgentSessions:Enabled";

    public async Task<AgentSessionSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var value = await db.ApplicationSettings.AsNoTracking()
            .Where(setting => setting.Key == EnabledKey)
            .Select(setting => setting.ValueJson).SingleAsync(cancellationToken);
        return new(JsonSerializer.Deserialize<bool>(value));
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        var valueJson = JsonSerializer.Serialize(enabled);
        var now = timeProvider.GetUtcNow();
        var affected = await db.ApplicationSettings.Where(setting => setting.Key == EnabledKey)
            .ExecuteUpdateAsync(update => update
                .SetProperty(setting => setting.ValueJson, valueJson)
                .SetProperty(setting => setting.UpdatedAt, now), cancellationToken);
        if (affected != 1)
        {
            throw new InvalidOperationException(InfrastructureErrorCodes.AgentSessionSettingMissing);
        }

        await snapshotLoader.RefreshAsync(cancellationToken);
    }
}
