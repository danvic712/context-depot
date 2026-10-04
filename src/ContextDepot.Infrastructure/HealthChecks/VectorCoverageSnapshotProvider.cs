using System.Text.Json;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Infrastructure.Options;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class VectorCoverageSnapshotProvider(
    ContextDepotDbContext db,
    IVectorIndexRepository vectorIndexRepository,
    ContextEmbeddingTextBuilder contextTextBuilder,
    DocumentEmbeddingTextBuilder documentTextBuilder,
    IMemoryCache cache,
    ScopedInferenceRuntimeSnapshot inferenceSnapshot,
    IOptionsMonitor<VectorCoverageOptions> options,
    VectorCoverageComputationGate computationGate)
{
    private const int HashLookupBatchSize = 256;
    private readonly string profileFingerprint = inferenceSnapshot.Value.Embedding?.ProfileFingerprint ?? string.Empty;

    public async Task<VectorCoverageSnapshot> GetAsync(
        Guid depotId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshots = await GetAsync([depotId], now, cancellationToken);
        return snapshots[depotId];
    }

    public async Task<IReadOnlyDictionary<Guid, VectorCoverageSnapshot>> GetAsync(
        IReadOnlyCollection<Guid> depotIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(depotIds);
        var cacheDuration = TimeSpan.FromSeconds(options.CurrentValue.CacheDurationSeconds);
        var ids = depotIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, VectorCoverageSnapshot>();
        }

        var snapshots = new Dictionary<Guid, VectorCoverageSnapshot>(ids.Length);
        var missingIds = new List<Guid>(ids.Length);
        foreach (var depotId in ids)
        {
            if (cache.TryGetValue<VectorCoverageSnapshot>(GetCacheKey(depotId), out var cached) && cached is not null)
            {
                snapshots[depotId] = cached;
            }
            else
            {
                missingIds.Add(depotId);
            }
        }

        if (missingIds.Count == 0)
        {
            return snapshots;
        }

        await computationGate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            // A different request may have populated the cache while this one waited.
            var stillMissing = new List<Guid>();
            foreach (var depotId in missingIds)
            {
                if (cache.TryGetValue<VectorCoverageSnapshot>(GetCacheKey(depotId), out var cached) && cached is not null)
                    snapshots[depotId] = cached;
                else
                    stillMissing.Add(depotId);
            }
            if (stillMissing.Count == 0) return snapshots;
            var computed = await ComputeAsync(stillMissing, now, cancellationToken);
            foreach (var (depotId, snapshot) in computed)
            {
                cache.Set(GetCacheKey(depotId), snapshot, cacheDuration);
                snapshots[depotId] = snapshot;
            }
        }
        finally
        {
            computationGate.Semaphore.Release();
        }

        return snapshots;
    }

    private async Task<IReadOnlyDictionary<Guid, VectorCoverageSnapshot>> ComputeAsync(
        IReadOnlyList<Guid> depotIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking()
            .Where(workspace => depotIds.Contains(workspace.DepotId))
            .ToListAsync(cancellationToken);
        var workspacePathsByDepot = workspaces
            .GroupBy(workspace => workspace.DepotId)
            .ToDictionary(group => group.Key, group => WorkspacePath.BuildPaths(group));

        var contextSources = db.ContextItems.AsNoTracking()
            .WhereRetrievableAt(now).Where(context => depotIds.Contains(context.DepotId));
        var documentSources = db.DocumentChunks.AsNoTracking()
            .Where(chunk => depotIds.Contains(chunk.DepotId) && chunk.Document != null &&
                chunk.Document.Status == DocumentStatus.Active && chunk.Document.IndexStatus == DocumentIndexStatus.Indexed);
        var contextTotals = new Dictionary<Guid, int>();
        var documentTotals = new Dictionary<Guid, int>();
        var contextIndexedByDepot = new Dictionary<Guid, int>();
        var documentIndexedByDepot = new Dictionary<Guid, int>();
        Guid? contextAfter = null;
        while (true)
        {
            var pageQuery = contextAfter is Guid cursor ? contextSources.Where(source => source.Id.CompareTo(cursor) > 0) : contextSources;
            var page = await pageQuery.OrderBy(source => source.Id).Take(HashLookupBatchSize)
                .Select(context => new ContextCoverageSource(context.Id, context.DepotId, context.WorkspaceId,
                    context.Kind, context.Key, context.Title, context.TagsJson, context.Content)).ToListAsync(cancellationToken);
            if (page.Count == 0) break;
            var hashes = await vectorIndexRepository.GetContextInputHashesAsync(page.Select(source => source.Id).ToArray(), cancellationToken);
            foreach (var context in page)
            {
                contextTotals[context.DepotId] = contextTotals.GetValueOrDefault(context.DepotId) + 1;
                if (!workspacePathsByDepot.TryGetValue(context.DepotId, out var paths) ||
                    !paths.TryGetValue(context.WorkspaceId, out var path) || !hashes.TryGetValue(context.Id, out var stored)) continue;
                var hash = EmbeddingInputHash.Compute(contextTextBuilder.Build(new ContextEmbeddingSource(
                    path, context.Kind, context.Key, context.Title, ParseTags(context.TagsJson), context.Content)));
                if (string.Equals(stored, hash, StringComparison.OrdinalIgnoreCase))
                    contextIndexedByDepot[context.DepotId] = contextIndexedByDepot.GetValueOrDefault(context.DepotId) + 1;
            }
            if (page.Count < HashLookupBatchSize) break;
            contextAfter = page[^1].Id;
        }
        Guid? documentAfter = null;
        while (true)
        {
            var pageQuery = documentAfter is Guid cursor ? documentSources.Where(source => source.Id.CompareTo(cursor) > 0) : documentSources;
            var page = await pageQuery.OrderBy(source => source.Id).Take(HashLookupBatchSize)
                .Select(chunk => new DocumentCoverageSource(chunk.Id, chunk.DepotId, chunk.WorkspaceId,
                    chunk.Document!.Path, chunk.Document.Title, chunk.HeadingPath, chunk.Content)).ToListAsync(cancellationToken);
            if (page.Count == 0) break;
            var hashes = await vectorIndexRepository.GetDocumentInputHashesAsync(page.Select(source => source.Id).ToArray(), cancellationToken);
            foreach (var document in page)
            {
                documentTotals[document.DepotId] = documentTotals.GetValueOrDefault(document.DepotId) + 1;
                if (!workspacePathsByDepot.TryGetValue(document.DepotId, out var paths) ||
                    !paths.TryGetValue(document.WorkspaceId, out var path) || !hashes.TryGetValue(document.Id, out var stored)) continue;
                var hash = EmbeddingInputHash.Compute(documentTextBuilder.Build(new DocumentChunkEmbeddingSource(
                    path, document.Path, document.Title, document.HeadingPath, document.Content)));
                if (string.Equals(stored, hash, StringComparison.OrdinalIgnoreCase))
                    documentIndexedByDepot[document.DepotId] = documentIndexedByDepot.GetValueOrDefault(document.DepotId) + 1;
            }
            if (page.Count < HashLookupBatchSize) break;
            documentAfter = page[^1].Id;
        }
        return depotIds.ToDictionary(
            depotId => depotId,
            depotId => new VectorCoverageSnapshot(
                contextTotals.GetValueOrDefault(depotId),
                contextIndexedByDepot.GetValueOrDefault(depotId),
                documentTotals.GetValueOrDefault(depotId),
                documentIndexedByDepot.GetValueOrDefault(depotId)));
    }

    private string GetCacheKey(Guid depotId) => $"context-depot:vector-coverage:{profileFingerprint}:{depotId:N}";

    private static IReadOnlyList<string> ParseTags(string tagsJson) =>
        JsonSerializer.Deserialize<string[]>(tagsJson) ?? [];

}
