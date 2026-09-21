using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class VectorCoverageHealthCheck(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentDepotAccessContext>().AllowInternalAccess();
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            var snapshotProvider = scope.ServiceProvider.GetRequiredService<VectorCoverageSnapshotProvider>();
            var depotIds = await db.Depots
                .AsNoTracking()
                .OrderBy(depot => depot.Id)
                .Select(depot => depot.Id)
                .ToListAsync(cancellationToken);
            var snapshots = new List<VectorCoverageSnapshot>(depotIds.Count);
            foreach (var depotId in depotIds)
            {
                snapshots.Add(await snapshotProvider.GetAsync(
                    depotId,
                    timeProvider.GetUtcNow(),
                    cancellationToken));
            }

            var contextTotal = snapshots.Sum(snapshot => snapshot.ContextTotal);
            var contextIndexed = snapshots.Sum(snapshot => snapshot.ContextIndexed);
            var documentChunkTotal = snapshots.Sum(snapshot => snapshot.DocumentChunkTotal);
            var documentChunkIndexed = snapshots.Sum(snapshot => snapshot.DocumentChunkIndexed);
            var isComplete = snapshots.All(snapshot => snapshot.IsComplete);
            var contextCoverage = CalculateCoverage(contextTotal, contextIndexed);
            var documentCoverage = CalculateCoverage(documentChunkTotal, documentChunkIndexed);
            var data = new Dictionary<string, object>
            {
                ["depot_count"] = depotIds.Count,
                ["context_total"] = contextTotal,
                ["context_indexed"] = contextIndexed,
                ["context_coverage"] = contextCoverage,
                ["document_chunk_total"] = documentChunkTotal,
                ["document_chunk_indexed"] = documentChunkIndexed,
                ["document_coverage"] = documentCoverage,
                ["retrieval_degraded"] = !isComplete
            };
            return isComplete
                ? HealthCheckResult.Healthy("Vector coverage is complete for all depots.", data)
                : HealthCheckResult.Degraded(
                    "Vector coverage is still being repaired for one or more depots.",
                    data: data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded(
                "Vector coverage is temporarily unavailable.",
                exception,
                new Dictionary<string, object> { ["retrieval_degraded"] = true });
        }
    }

    private static double CalculateCoverage(int total, int indexed) =>
        total == 0 ? 1 : Math.Clamp(indexed / (double)total, 0, 1);
}
