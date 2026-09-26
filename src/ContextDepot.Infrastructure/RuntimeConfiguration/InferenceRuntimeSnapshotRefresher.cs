using ContextDepot.Infrastructure.VectorStore;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed class InferenceRuntimeSnapshotRefresher(
    InferenceRuntimeSnapshotLoader loader,
    VectorCollectionInitializer collectionInitializer,
    InferenceRuntimeSnapshotAccessor snapshotAccessor,
    ILogger<InferenceRuntimeSnapshotRefresher> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private InferenceRuntimeSnapshot? lastPublished;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await loader.LoadAsync(cancellationToken);
            if (snapshot == lastPublished)
            {
                return;
            }

            // Make a new vector profile usable before requests can observe it.
            await collectionInitializer.InitializeAsync(snapshot.Embedding, cancellationToken);
            snapshotAccessor.Publish(snapshot);
            lastPublished = snapshot;

            if (snapshot.State == InferenceRuntimeState.Ready)
            {
                logger.LogInformation("Loaded the configured embedding inference route from the database.");
            }
            else if (snapshot.State == InferenceRuntimeState.Unconfigured)
            {
                logger.LogWarning("No embedding inference route is configured; semantic retrieval is disabled.");
            }
            else
            {
                logger.LogWarning(
                    "The embedding inference route is unavailable ({DegradedReason}); semantic retrieval is disabled.",
                    snapshot.DegradedReason);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
