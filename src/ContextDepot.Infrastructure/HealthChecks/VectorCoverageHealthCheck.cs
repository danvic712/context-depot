using ContextDepot.Application.Shared.Runtime.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class VectorCoverageHealthCheck(
    VectorCoverageSnapshotProvider snapshotProvider,
    ICurrentOwnerContext currentOwner,
    TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await snapshotProvider.GetAsync(
                currentOwner.OwnerId,
                timeProvider.GetUtcNow(),
                cancellationToken);
            var data = new Dictionary<string, object>
            {
                ["context_total"] = snapshot.ContextTotal,
                ["context_indexed"] = snapshot.ContextIndexed,
                ["context_coverage"] = snapshot.ContextCoverage,
                ["document_chunk_total"] = snapshot.DocumentChunkTotal,
                ["document_chunk_indexed"] = snapshot.DocumentChunkIndexed,
                ["document_coverage"] = snapshot.DocumentCoverage,
                ["retrieval_degraded"] = !snapshot.IsComplete
            };
            return snapshot.IsComplete
                ? HealthCheckResult.Healthy("Active profile vector coverage is complete.", data)
                : HealthCheckResult.Degraded(
                    "Active profile vector coverage is still being repaired.",
                    data: data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded(
                "Active profile vector coverage is temporarily unavailable.",
                exception,
                new Dictionary<string, object> { ["retrieval_degraded"] = true });
        }
    }
}
