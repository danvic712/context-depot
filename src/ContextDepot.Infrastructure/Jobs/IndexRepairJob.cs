using ContextDepot.Application.IndexRepair;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class IndexRepairJob(IndexRepairCycleRunner runner, IOptionsMonitor<IndexRepairOptions> options,
    ILogger<IndexRepairJob> logger) : IDisposable
{
    private readonly BackgroundJobExecutionGate gate = new(logger);

    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        gate.RunAsync(ct => runner.RunAsync(options.CurrentValue, ct), cancellationToken);

    public void Dispose() => gate.Dispose();
}
