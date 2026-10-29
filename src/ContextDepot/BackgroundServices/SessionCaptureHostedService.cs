using ContextDepot.Application.AgentSessions;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.Extensions.Options;

namespace ContextDepot.BackgroundServices;

public sealed class SessionCaptureHostedService(IOptions<SessionCaptureOptions> options,
    SessionCaptureCycleRunner runner, ILogger<SessionCaptureHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
                await runner.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                logger.LogWarning("Session capture queue unavailable; the next cycle will retry.");
            }
        }
    }
}
