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
    IOptionsMonitor<VectorCoverageOptions> options)
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

        var computed = await ComputeAsync(missingIds, now, cancellationToken);
        foreach (var (depotId, snapshot) in computed)
        {
            cache.Set(GetCacheKey(depotId), snapshot, cacheDuration);
            snapshots[depotId] = snapshot;
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

        // Load all depots in one pass. The health check used to repeat these queries
        // once per depot, which made probe cost grow linearly with both depot count
        // and the amount of data in each depot.
        var contexts = await db.ContextItems.AsNoTracking()
            .WhereRetrievableAt(now)
            .Where(context => depotIds.Contains(context.DepotId))
            .Select(context => new ContextCoverageSource(
                context.Id,
                context.DepotId,
                context.WorkspaceId,
                context.Kind,
                context.Key,
                context.Title,
                context.TagsJson,
                context.Content))
            .ToListAsync(cancellationToken);
        var documents = await db.DocumentChunks.AsNoTracking()
            .Where(chunk => depotIds.Contains(chunk.DepotId) &&
                           chunk.Document != null &&
                           chunk.Document.Status == DocumentStatus.Active &&
                           chunk.Document.IndexStatus == DocumentIndexStatus.Indexed)
            .Select(chunk => new DocumentCoverageSource(
                chunk.Id,
                chunk.DepotId,
                chunk.WorkspaceId,
                chunk.Document!.Path,
                chunk.Document.Title,
                chunk.HeadingPath,
                chunk.Content))
            .ToListAsync(cancellationToken);
        var contextHashes = await ReadContextHashesAsync(contexts.Select(context => context.Id).ToArray(), cancellationToken);
        var documentHashes = await ReadDocumentHashesAsync(documents.Select(document => document.Id).ToArray(), cancellationToken);

        var contextIndexedByDepot = new Dictionary<Guid, int>();
        foreach (var context in contexts)
        {
            if (!workspacePathsByDepot.TryGetValue(context.DepotId, out var workspacePaths) ||
                !workspacePaths.TryGetValue(context.WorkspaceId, out var workspacePath) ||
                !contextHashes.TryGetValue(context.Id, out var storedHash))
            {
                continue;
            }

            var currentHash = EmbeddingInputHash.Compute(contextTextBuilder.Build(new ContextEmbeddingSource(
                workspacePath,
                context.Kind,
                context.Key,
                context.Title,
                ParseTags(context.TagsJson),
                context.Content)));
            if (string.Equals(storedHash, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                contextIndexedByDepot[context.DepotId] = contextIndexedByDepot.GetValueOrDefault(context.DepotId) + 1;
            }
        }

        var documentIndexedByDepot = new Dictionary<Guid, int>();
        foreach (var document in documents)
        {
            if (!workspacePathsByDepot.TryGetValue(document.DepotId, out var workspacePaths) ||
                !workspacePaths.TryGetValue(document.WorkspaceId, out var workspacePath) ||
                !documentHashes.TryGetValue(document.Id, out var storedHash))
            {
                continue;
            }

            var currentHash = EmbeddingInputHash.Compute(documentTextBuilder.Build(new DocumentChunkEmbeddingSource(
                workspacePath,
                document.Path,
                document.Title,
                document.HeadingPath,
                document.Content)));
            if (string.Equals(storedHash, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                documentIndexedByDepot[document.DepotId] = documentIndexedByDepot.GetValueOrDefault(document.DepotId) + 1;
            }
        }

        var contextTotals = contexts.GroupBy(context => context.DepotId)
            .ToDictionary(group => group.Key, group => group.Count());
        var documentTotals = documents.GroupBy(document => document.DepotId)
            .ToDictionary(group => group.Key, group => group.Count());
        return depotIds.ToDictionary(
            depotId => depotId,
            depotId => new VectorCoverageSnapshot(
                contextTotals.GetValueOrDefault(depotId),
                contextIndexedByDepot.GetValueOrDefault(depotId),
                documentTotals.GetValueOrDefault(depotId),
                documentIndexedByDepot.GetValueOrDefault(depotId)));
    }

    private string GetCacheKey(Guid depotId) => $"context-depot:vector-coverage:{profileFingerprint}:{depotId:N}";

    private async Task<IReadOnlyDictionary<Guid, string>> ReadContextHashesAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var batch in ids.Chunk(HashLookupBatchSize))
        {
            var hashes = await vectorIndexRepository.GetContextInputHashesAsync(batch, cancellationToken);
            foreach (var hash in hashes)
            {
                result[hash.Key] = hash.Value;
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ReadDocumentHashesAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var batch in ids.Chunk(HashLookupBatchSize))
        {
            var hashes = await vectorIndexRepository.GetDocumentInputHashesAsync(batch, cancellationToken);
            foreach (var hash in hashes)
            {
                result[hash.Key] = hash.Value;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ParseTags(string tagsJson) =>
        JsonSerializer.Deserialize<string[]>(tagsJson) ?? [];

}
