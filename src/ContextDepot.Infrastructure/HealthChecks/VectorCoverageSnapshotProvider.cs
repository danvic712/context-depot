using System.Text.Json;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class VectorCoverageSnapshotProvider(
    ContextDepotDbContext db,
    IVectorIndexRepository vectorIndexRepository,
    ContextEmbeddingTextBuilder contextTextBuilder,
    DocumentEmbeddingTextBuilder documentTextBuilder)
{
    private const int HashLookupBatchSize = 256;

    public async Task<VectorCoverageSnapshot> GetAsync(
        Guid depotId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var workspacePaths = await GetWorkspacePathsAsync(depotId, cancellationToken);
        var contexts = await db.ContextItems.AsNoTracking()
            .Where(context => context.DepotId == depotId &&
                              context.Status == ContextStatus.Active &&
                              (context.ExpiresAt == null || context.ExpiresAt > now))
            .Select(context => new ContextCoverageSource(
                context.Id,
                context.WorkspaceId,
                context.Kind,
                context.Key,
                context.Title,
                context.TagsJson,
                context.Content))
            .ToListAsync(cancellationToken);
        var documents = await db.DocumentChunks.AsNoTracking()
            .Where(chunk => chunk.DepotId == depotId &&
                           chunk.Document != null &&
                           chunk.Document.Status == DocumentStatus.Active &&
                           chunk.Document.IndexStatus == DocumentIndexStatus.Indexed)
            .Select(chunk => new DocumentCoverageSource(
                chunk.Id,
                chunk.WorkspaceId,
                chunk.Document!.Path,
                chunk.Document.Title,
                chunk.HeadingPath,
                chunk.Content))
            .ToListAsync(cancellationToken);
        var contextHashes = await ReadContextHashesAsync(contexts.Select(context => context.Id).ToArray(), cancellationToken);
        var documentHashes = await ReadDocumentHashesAsync(documents.Select(document => document.Id).ToArray(), cancellationToken);
        var contextIndexed = contexts.Count(context =>
            workspacePaths.TryGetValue(context.WorkspaceId, out var workspacePath) &&
            contextHashes.TryGetValue(context.Id, out var storedHash) &&
            string.Equals(
                storedHash,
                EmbeddingInputHash.Compute(contextTextBuilder.Build(new ContextEmbeddingSource(
                    workspacePath,
                    context.Kind,
                    context.Key,
                    context.Title,
                    ParseTags(context.TagsJson),
                    context.Content))),
                StringComparison.OrdinalIgnoreCase));
        var documentIndexed = documents.Count(document =>
            workspacePaths.TryGetValue(document.WorkspaceId, out var workspacePath) &&
            documentHashes.TryGetValue(document.Id, out var storedHash) &&
            string.Equals(
                storedHash,
                EmbeddingInputHash.Compute(documentTextBuilder.Build(new DocumentChunkEmbeddingSource(
                    workspacePath,
                    document.Path,
                    document.Title,
                    document.HeadingPath,
                    document.Content))),
                StringComparison.OrdinalIgnoreCase));
        return new VectorCoverageSnapshot(
            contexts.Count,
            contextIndexed,
            documents.Count,
            documentIndexed);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetWorkspacePathsAsync(
        Guid depotId,
        CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking()
            .Where(workspace => workspace.DepotId == depotId)
            .ToListAsync(cancellationToken);
        return WorkspacePath.BuildPaths(workspaces);
    }

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
