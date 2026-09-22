using ContextDepot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure;

public sealed class DatabaseApplicationSettingsReloadService(
    IServiceScopeFactory scopeFactory,
    DatabaseApplicationSettingsConfigurationProvider configurationProvider,
    DatabaseApplicationSettingsSnapshotBuilder snapshotBuilder,
    ILogger<DatabaseApplicationSettingsReloadService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public Task LoadInitialAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Database application settings could not be refreshed; the last valid snapshot remains active.");
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
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
