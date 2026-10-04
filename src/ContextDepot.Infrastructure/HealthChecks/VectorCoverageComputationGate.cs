namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class VectorCoverageComputationGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public void Dispose() => Semaphore.Dispose();
}
