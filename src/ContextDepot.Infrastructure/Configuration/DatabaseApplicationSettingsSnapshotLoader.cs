using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Configuration;

public sealed class DatabaseApplicationSettingsSnapshotLoader(
    IServiceScopeFactory scopeFactory,
    DatabaseApplicationSettingsConfigurationProvider configurationProvider,
    DatabaseApplicationSettingsSnapshotBuilder snapshotBuilder,
    ILogger<DatabaseApplicationSettingsSnapshotLoader> logger)
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            var records = await db.ApplicationSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Key)
                .ToArrayAsync(cancellationToken);
            var snapshot = snapshotBuilder.Build(records);
            if (configurationProvider.HasSameSnapshot(snapshot))
            {
                return;
            }

            configurationProvider.Publish(snapshot);
            logger.LogInformation(
                "Published a validated database application settings snapshot with {SettingCount} settings.",
                snapshot.Count);
        }
        finally
        {
            refreshLock.Release();
        }
    }
}
