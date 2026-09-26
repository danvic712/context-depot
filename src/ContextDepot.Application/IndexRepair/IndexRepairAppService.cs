using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
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
    VectorIndexRepairer vectorIndexRepairer,
    TimeProvider timeProvider,
    ILogger<IndexRepairAppService> logger) : IIndexRepairAppService
{
    private readonly CanonicalDocumentIndexer indexer = new(documentRepository, markdownStore, chunker, sourceSafety, idGenerator, timeProvider);

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

        if (!request.RepairVectors)
        {
            return new IndexRepairCycleResult(
                documentsReconciled, 0, 0,
                request.ContextAfterId, request.DocumentChunkAfterId,
                false, false, true);
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
                    var result = await vectorIndexRepairer.RepairContextsAsync(contextPage, cancellationToken);
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
                    var result = await vectorIndexRepairer.RepairDocumentsAsync(documentPage, cancellationToken);
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
            markdown = await indexer.ReadAsync(
                depotId,
                candidate.WorkspacePath,
                candidate.Path,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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
            await indexer.ReconcileAsync(new CanonicalDocumentSource(
                candidate.DocumentId,
                depotId,
                candidate.WorkspaceId,
                candidate.WorkspacePath,
                candidate.Path,
                candidate.Title,
                markdown), cancellationToken);
            return (true, false);
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

    private static void ValidateRequest(Guid depotId, IndexRepairRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (depotId == Guid.Empty || request.BatchSize is < 1 or > 256 || request.MaxBatches is < 1 or > 100)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.IndexRepairRequestInvalid);
        }
    }
}
