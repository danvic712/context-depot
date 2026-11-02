using ContextDepot.Infrastructure.AgentSessions;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class SessionCaptureJob(SessionCaptureCycleRunner runner, ILogger<SessionCaptureJob> logger) : IDisposable
{
    private readonly BackgroundJobExecutionGate gate = new(logger);

    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task ExecuteAsync(CancellationToken cancellationToken) => gate.RunAsync(runner.RunAsync, cancellationToken);

    public void Dispose() => gate.Dispose();
}
