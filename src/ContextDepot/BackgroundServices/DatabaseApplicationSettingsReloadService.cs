using ContextDepot.Infrastructure.RuntimeConfiguration;

namespace ContextDepot.BackgroundServices;

public sealed class DatabaseApplicationSettingsReloadService(
    DatabaseApplicationSettingsSnapshotLoader snapshotLoader,
    ILogger<DatabaseApplicationSettingsReloadService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await snapshotLoader.RefreshAsync(stoppingToken);
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
}
