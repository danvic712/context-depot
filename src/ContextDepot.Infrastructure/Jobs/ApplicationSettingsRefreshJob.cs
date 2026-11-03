using ContextDepot.Infrastructure.RuntimeConfiguration;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class ApplicationSettingsRefreshJob(DatabaseApplicationSettingsSnapshotLoader loader,
    ILogger<ApplicationSettingsRefreshJob> logger) : IDisposable
{
    private readonly BackgroundJobExecutionGate gate = new(logger);

    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task ExecuteAsync(CancellationToken cancellationToken) => gate.RunAsync(loader.RefreshAsync, cancellationToken);

    public void Dispose() => gate.Dispose();
}
