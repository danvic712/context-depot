using ContextDepot.Infrastructure.RuntimeConfiguration;

namespace ContextDepot.BackgroundServices;

public sealed class InferenceRuntimeReloadService(
    InferenceRuntimeSnapshotRefresher refresher,
    ILogger<InferenceRuntimeReloadService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await refresher.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "The embedding inference route could not be refreshed; the last valid snapshot remains active.");
            }
        }
    }
}
