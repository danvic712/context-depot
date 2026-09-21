using System.Text.Json;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Application.VectorIndex.Dtos;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Application.IndexRepair;

public sealed class IndexRepairAppService(
    IIndexRepairRepository repairRepository,
    IDocumentRepository documentRepository,
    IMarkdownStore markdownStore,
    HeadingAwareMarkdownChunker chunker,
    DocumentWriteCoordinator documentWriteCoordinator,
    ISourceSafetyService sourceSafety,
    IIdGenerator idGenerator,
    IVectorIndexRepository vectorIndexRepository,
    EmbeddingGeneratorService embeddingGenerator,
    ContextEmbeddingTextBuilder contextTextBuilder,
    DocumentEmbeddingTextBuilder documentTextBuilder,
    TimeProvider timeProvider,
    ILogger<IndexRepairAppService> logger) : IIndexRepairAppService
{
    public async Task<IndexRepairCycleResult> RepairAsync(
        Guid depotId,
        IndexRepairRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(depotId, request);

        var documentsReconciled = 0;
        var retrievalDegraded = false;
        var documentCandidates = await repairRepository.FindDocumentIndexRepairCandidatesAsync(
            depotId,
            request.BatchSize,
            cancellationToken);
        foreach (var candidate in documentCandidates)
        {
            var result = await RepairDocumentAsync(depotId, candidate, cancellationToken);
            if (result.Reconciled)
            {
                documentsReconciled++;
            }

            retrievalDegraded |= result.Degraded;
        }

        var contextAfterId = request.ContextAfterId;
        var documentChunkAfterId = request.DocumentChunkAfterId;
        var contextScanWrapped = false;
        var documentScanWrapped = false;
        var contextFinished = false;
        var documentFinished = false;
        var contextVectorsCreatedOrUpdated = 0;
        var documentVectorsCreatedOrUpdated = 0;

        for (var batch = 0; batch < request.MaxBatches; batch++)
        {
            if (!contextFinished)
            {
                var contextPage = await repairRepository.FindContextSourcePageAsync(
                    depotId,
                    contextAfterId,
                    request.BatchSize,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                if (contextPage.Count == 0)
                {
                    contextAfterId = null;
                    contextScanWrapped = true;
                    contextFinished = true;
                }
                else
                {
                    var result = await RepairContextsAsync(contextPage, cancellationToken);
                    contextVectorsCreatedOrUpdated += result.Updated;
                    retrievalDegraded |= result.Degraded;
                    if (contextPage.Count < request.BatchSize)
                    {
                        contextAfterId = null;
                        contextScanWrapped = true;
                        contextFinished = true;
                    }
                    else
                    {
                        contextAfterId = contextPage[^1].ContextItemId;
                    }
                }
            }

            if (!documentFinished)
            {
                var documentPage = await repairRepository.FindDocumentChunkSourcePageAsync(
                    depotId,
                    documentChunkAfterId,
                    request.BatchSize,
                    cancellationToken);
                if (documentPage.Count == 0)
                {
                    documentChunkAfterId = null;
                    documentScanWrapped = true;
                    documentFinished = true;
                }
                else
                {
                    var result = await RepairDocumentsAsync(documentPage, cancellationToken);
                    documentVectorsCreatedOrUpdated += result.Updated;
                    retrievalDegraded |= result.Degraded;
                    if (documentPage.Count < request.BatchSize)
                    {
                        documentChunkAfterId = null;
                        documentScanWrapped = true;
                        documentFinished = true;
                    }
                    else
                    {
                        documentChunkAfterId = documentPage[^1].DocumentChunkId;
                    }
                }
            }

            if (contextFinished && documentFinished)
            {
                break;
            }
        }

        return new IndexRepairCycleResult(
            documentsReconciled,
            contextVectorsCreatedOrUpdated,
            documentVectorsCreatedOrUpdated,
            contextAfterId,
            documentChunkAfterId,
            contextScanWrapped,
            documentScanWrapped,
            retrievalDegraded);
    }

    private async Task<(bool Reconciled, bool Degraded)> RepairDocumentAsync(
        Guid depotId,
        DocumentIndexRepairCandidate candidate,
        CancellationToken cancellationToken)
    {
        await using var writeLease = await documentWriteCoordinator.AcquireAsync(
            depotId + ":" + candidate.WorkspaceId + ":" + candidate.Path,
            cancellationToken);

        var document = await documentRepository.GetByIdAsync(
            depotId,
            candidate.DocumentId,
            cancellationToken);
        if (document is null ||
            document.Status != DocumentStatus.Active ||
            document.IndexStatus is not (DocumentIndexStatus.Pending or DocumentIndexStatus.Failed))
        {
            return (false, false);
        }

        MarkdownDocument? markdown;
        try
        {
            markdown = await markdownStore.GetAsync(
                depotId,
                candidate.WorkspacePath + "/" + candidate.Path,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException exception)
        {
            logger.LogWarning(
                exception,
                "{ErrorCode} prevented document repair for {DocumentId}.",
                ApplicationErrorCodes.MarkdownRootUnavailable,
                candidate.DocumentId);
            await repairRepository.MarkDocumentIndexFailedAsync(
                depotId,
                candidate.DocumentId,
                ApplicationErrorCodes.MarkdownRootUnavailable,
                cancellationToken);
            return (false, true);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(
                exception,
                "{ErrorCode} prevented document repair for {DocumentId}.",
                ApplicationErrorCodes.MarkdownRootUnavailable,
                candidate.DocumentId);
            await repairRepository.MarkDocumentIndexFailedAsync(
                depotId,
                candidate.DocumentId,
                ApplicationErrorCodes.MarkdownRootUnavailable,
                cancellationToken);
            return (false, true);
        }

        if (markdown is null)
        {
            await repairRepository.MarkDocumentIndexFailedAsync(
                depotId,
                candidate.DocumentId,
                ApplicationErrorCodes.MarkdownFileMissing,
                cancellationToken);
            return (false, true);
        }

        try
        {
            sourceSafety.EnsureSafe(candidate.WorkspacePath);
            sourceSafety.EnsureSafe(candidate.Path);
            sourceSafety.EnsureSafe(candidate.Title);
            sourceSafety.EnsureSafe(markdown.Content);
        }
        catch (ContextDepotApplicationException exception)
        {
            await repairRepository.MarkDocumentIndexFailedAsync(
                depotId,
                candidate.DocumentId,
                exception.ErrorCode,
                cancellationToken);
            return (false, true);
        }

        var chunks = chunker.Chunk(markdown.Content)
            .Select((chunk, ordinal) => new DocumentChunkWrite(
                idGenerator.NewId(),
                ordinal,
                chunk.HeadingPath,
                chunk.Content,
                chunk.ContentHash))
            .ToArray();
        var write = new DocumentIndexWrite(
            candidate.DocumentId,
            depotId,
            candidate.WorkspaceId,
            candidate.Path,
            candidate.Title,
            markdown.ContentHash,
            chunks,
            timeProvider.GetUtcNow());

        try
        {
            await documentRepository.ReconcileIndexAsync(write, cancellationToken);
            return (true, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "{ErrorCode} prevented document repair for {DocumentId}; the next cycle will retry.",
                ApplicationErrorCodes.DocumentWriteFailed,
                candidate.DocumentId);
            return (false, true);
        }
    }

    private async Task<(int Updated, bool Degraded)> RepairContextsAsync(
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

    private async Task<(int Updated, bool Degraded)> RepairDocumentsAsync(
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

    private static void ValidateRequest(Guid depotId, IndexRepairRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (depotId == Guid.Empty || request.BatchSize is < 1 or > 256 || request.MaxBatches is < 1 or > 100)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.IndexRepairRequestInvalid);
        }
    }
}
