using ContextDepot.Application.IndexRepair;
using ContextDepot.Infrastructure.Jobs;
using Microsoft.Extensions.Options;

namespace ContextDepot.BackgroundServices;

public sealed class IndexRepairHostedService(
    IOptionsMonitor<IndexRepairOptions> options,
    IndexRepairCycleRunner cycleRunner,
    ILogger<IndexRepairHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var repairOptions = options.CurrentValue;
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(repairOptions.PollIntervalSeconds),
                    stoppingToken);
                await cycleRunner.RunAsync(repairOptions, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Index repair cycle failed; the current cursor will be retried.");
            }
        }
    }
}
