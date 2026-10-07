using Microsoft.Extensions.DependencyInjection;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Infrastructure.HealthChecks;
using ContextDepot.Infrastructure.Markdown;
using ContextDepot.Infrastructure.RuntimeConfiguration;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SettingsOverviewRepository(ContextDepotDbContext db, FileSystemMarkdownStore markdown,
    ScopedInferenceRuntimeSnapshot inference, IServiceProvider services, TimeProvider clock) : ISettingsOverviewRepository
{
    public async Task<SettingsOverviewDto> GetAsync(Guid depotId, string depotName, CancellationToken cancellationToken)
    {
        var databaseState = await db.Database.CanConnectAsync(cancellationToken) ? "available" : "unavailable";
        var semanticState = inference.Value.State == InferenceRuntimeState.Ready ? "configured"
            : inference.Value.State == InferenceRuntimeState.Unconfigured ? "unconfigured" : "degraded";
        var indexState = "unconfigured";
        int? indexed = null, total = null;
        if (inference.Value.Embedding is not null)
        {
            try
            {
                var snapshot = await services.GetRequiredService<VectorCoverageSnapshotProvider>().GetAsync(depotId, clock.GetUtcNow(), cancellationToken);
                indexed = snapshot.ContextIndexed + snapshot.DocumentChunkIndexed;
                total = snapshot.ContextTotal + snapshot.DocumentChunkTotal;
                indexState = snapshot.IsComplete ? "complete" : "repairing";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                indexState = "unknown";
            }
        }
        return new SettingsOverviewDto(depotName, databaseState, markdown.CanReadAndWrite() ? "available" : "unavailable",
            semanticState, indexState, indexed, total, "/mcp");
    }
}
