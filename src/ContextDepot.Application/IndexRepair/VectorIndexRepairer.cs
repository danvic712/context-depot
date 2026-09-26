using System.Text.Json;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Application.VectorIndex.Dtos;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Application.IndexRepair;

public sealed class VectorIndexRepairer(
    IVectorIndexRepository vectorIndexRepository,
    EmbeddingGeneratorService embeddingGenerator,
    ContextEmbeddingTextBuilder contextTextBuilder,
    DocumentEmbeddingTextBuilder documentTextBuilder,
    ILogger<VectorIndexRepairer> logger)
{
    public async Task<(int Updated, bool Degraded)> RepairContextsAsync(
        IReadOnlyList<ContextEmbeddingRepairCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var inputs = candidates
            .Select(candidate =>
            {
                var source = new ContextEmbeddingSource(
                    candidate.WorkspacePath,
                    candidate.Kind,
                    candidate.Key,
                    candidate.Title,
                    ParseTags(candidate.TagsJson),
                    candidate.Content);
                var text = contextTextBuilder.Build(source);
                return (Candidate: candidate, Text: text, Hash: EmbeddingInputHash.Compute(text));
            })
            .ToArray();

        IReadOnlyDictionary<Guid, string> storedHashes;
        try
        {
            storedHashes = await vectorIndexRepository.GetContextInputHashesAsync(
                inputs.Select(input => input.Candidate.ContextItemId).ToArray(),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Context vector hash repair was unavailable; the next cycle will retry.");
            return (0, true);
        }

        var stale = inputs
            .Where(input => !storedHashes.TryGetValue(input.Candidate.ContextItemId, out var stored) ||
                            !string.Equals(stored, input.Hash, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (stale.Length == 0)
        {
            return (0, false);
        }

        IReadOnlyList<ReadOnlyMemory<float>> vectors;
        try
        {
            vectors = await embeddingGenerator.GenerateAsync(
                stale.Select(input => input.Text).ToArray(),
                cancellationToken);
            var writes = stale.Select((input, index) => new ContextVectorIndexWrite(
                    input.Candidate.ContextItemId,
                    input.Candidate.DepotId,
                    input.Candidate.WorkspaceId,
                    input.Candidate.Kind,
                    input.Hash,
                    vectors[index]))
                .ToArray();
            await vectorIndexRepository.UpsertContextVectorsAsync(writes, cancellationToken);
            return (writes.Length, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Context vector repair was unavailable; the next cycle will retry.");
            return (0, true);
        }
    }

    public async Task<(int Updated, bool Degraded)> RepairDocumentsAsync(
        IReadOnlyList<DocumentEmbeddingRepairCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var inputs = candidates
            .Select(candidate =>
            {
                var source = new DocumentChunkEmbeddingSource(
                    candidate.WorkspacePath,
                    candidate.DocumentPath,
                    candidate.Title,
                    candidate.HeadingPath,
                    candidate.Content);
                var text = documentTextBuilder.Build(source);
                return (Candidate: candidate, Text: text, Hash: EmbeddingInputHash.Compute(text));
            })
            .ToArray();

        IReadOnlyDictionary<Guid, string> storedHashes;
        try
        {
            storedHashes = await vectorIndexRepository.GetDocumentInputHashesAsync(
                inputs.Select(input => input.Candidate.DocumentChunkId).ToArray(),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Document vector hash repair was unavailable; the next cycle will retry.");
            return (0, true);
        }

        var stale = inputs
            .Where(input => !storedHashes.TryGetValue(input.Candidate.DocumentChunkId, out var stored) ||
                            !string.Equals(stored, input.Hash, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (stale.Length == 0)
        {
            return (0, false);
        }

        IReadOnlyList<ReadOnlyMemory<float>> vectors;
        try
        {
            vectors = await embeddingGenerator.GenerateAsync(
                stale.Select(input => input.Text).ToArray(),
                cancellationToken);
            var writes = stale.Select((input, index) => new DocumentVectorIndexWrite(
                    input.Candidate.DocumentChunkId,
                    input.Candidate.DocumentId,
                    input.Candidate.DepotId,
                    input.Candidate.WorkspaceId,
                    input.Hash,
                    vectors[index]))
                .ToArray();
            await vectorIndexRepository.UpsertDocumentVectorsAsync(writes, cancellationToken);
            return (writes.Length, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Document vector repair was unavailable; the next cycle will retry.");
            return (0, true);
        }
    }

    private static IReadOnlyList<string> ParseTags(string tagsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(tagsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

}
