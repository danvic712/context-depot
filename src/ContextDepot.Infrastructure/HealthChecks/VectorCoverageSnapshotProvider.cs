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
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var key = $"context-depot:vector-coverage:{profileFingerprint}";
        if (cache.TryGetValue<VectorCoverageSnapshot>(key, out var cached) && cached is not null) return cached;

        await computationGate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<VectorCoverageSnapshot>(key, out cached) && cached is not null) return cached;
            var computed = await ComputeAsync(now, cancellationToken);
            cache.Set(key, computed, TimeSpan.FromSeconds(options.CurrentValue.CacheDurationSeconds));
            return computed;
        }
        finally
        {
            computationGate.Semaphore.Release();
        }
    }

    private async Task<VectorCoverageSnapshot> ComputeAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var workspacePaths = WorkspacePath.BuildPaths(await db.Workspaces.AsNoTracking().ToArrayAsync(cancellationToken));
        var contextSources = db.ContextItems.AsNoTracking().WhereRetrievableAt(now);
        var documentSources = db.DocumentChunks.AsNoTracking()
            .Where(chunk => chunk.Document != null && chunk.Document.Status == DocumentStatus.Active &&
                chunk.Document.IndexStatus == DocumentIndexStatus.Indexed);
        var contextTotal = 0;
        var documentTotal = 0;
        var contextIndexed = 0;
        var documentIndexed = 0;
        Guid? contextAfter = null;
        while (true)
        {
            var pageQuery = contextAfter is Guid cursor ? contextSources.Where(source => source.Id.CompareTo(cursor) > 0) : contextSources;
            var page = await pageQuery.OrderBy(source => source.Id).Take(HashLookupBatchSize)
                .Select(context => new ContextCoverageSource(context.Id, context.WorkspaceId,
                    context.Kind, context.Key, context.Title, context.TagsJson, context.Content)).ToListAsync(cancellationToken);
            if (page.Count == 0) break;
            var hashes = await vectorIndexRepository.GetContextInputHashesAsync(page.Select(source => source.Id).ToArray(), cancellationToken);
            foreach (var context in page)
            {
                contextTotal++;
                if (!workspacePaths.TryGetValue(context.WorkspaceId, out var path) || !hashes.TryGetValue(context.Id, out var stored)) continue;
                var hash = EmbeddingInputHash.Compute(contextTextBuilder.Build(new ContextEmbeddingSource(
                    path, context.Kind, context.Key, context.Title, ParseTags(context.TagsJson), context.Content)));
                if (string.Equals(stored, hash, StringComparison.OrdinalIgnoreCase))
                    contextIndexed++;
            }
            if (page.Count < HashLookupBatchSize) break;
            contextAfter = page[^1].Id;
        }
        Guid? documentAfter = null;
        while (true)
        {
            var pageQuery = documentAfter is Guid cursor ? documentSources.Where(source => source.Id.CompareTo(cursor) > 0) : documentSources;
            var page = await pageQuery.OrderBy(source => source.Id).Take(HashLookupBatchSize)
                .Select(chunk => new DocumentCoverageSource(chunk.Id, chunk.WorkspaceId,
                    chunk.Document!.Path, chunk.Document.Title, chunk.HeadingPath, chunk.Content)).ToListAsync(cancellationToken);
            if (page.Count == 0) break;
            var hashes = await vectorIndexRepository.GetDocumentInputHashesAsync(page.Select(source => source.Id).ToArray(), cancellationToken);
            foreach (var document in page)
            {
                documentTotal++;
                if (!workspacePaths.TryGetValue(document.WorkspaceId, out var path) || !hashes.TryGetValue(document.Id, out var stored)) continue;
                var hash = EmbeddingInputHash.Compute(documentTextBuilder.Build(new DocumentChunkEmbeddingSource(
                    path, document.Path, document.Title, document.HeadingPath, document.Content)));
                if (string.Equals(stored, hash, StringComparison.OrdinalIgnoreCase))
                    documentIndexed++;
            }
            if (page.Count < HashLookupBatchSize) break;
            documentAfter = page[^1].Id;
        }
        return new(contextTotal, contextIndexed, documentTotal, documentIndexed);
    }

    private static IReadOnlyList<string> ParseTags(string tagsJson) =>
        JsonSerializer.Deserialize<string[]>(tagsJson) ?? [];

}
