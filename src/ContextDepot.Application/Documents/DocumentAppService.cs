using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Documents.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Documents;

public sealed class DocumentAppService(
    ICurrentDepotContext currentDepot,
    IWorkspaceAppService workspaceAppService,
    IDocumentRepository repository,
    IMarkdownStore markdownStore,
    HeadingAwareMarkdownChunker chunker,
    DocumentWriteCoordinator coordinator,
    ISourceSafetyService sourceSafety,
    IIdGenerator idGenerator,
    TimeProvider timeProvider) : IDocumentAppService
{
    public async Task<DocumentModel> UpsertAsync(UpsertDocumentCommand command, CancellationToken cancellationToken)
    {
        var workspace = await workspaceAppService.ResolveAsync(command.Workspace, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        var normalizedPath = DocumentPath.Normalize(command.Path);
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentTitle);
        }

        sourceSafety.EnsureSafe(workspace.Path);
        sourceSafety.EnsureSafe(normalizedPath);
        sourceSafety.EnsureSafe(command.Content);
        sourceSafety.EnsureSafe(command.Title);
        var depotId = currentDepot.DepotId;
        var relativePath = workspace.Path + "/" + normalizedPath;
        var incomingHash = DocumentContentHasher.Compute(command.Content);
        await using var writeLock = await coordinator.AcquireAsync(depotId + ":" + workspace.Id + ":" + normalizedPath, cancellationToken);

        // Re-read both stores after acquiring the per-document lock. This prevents a stale
        // pre-lock read from overwriting a concurrent writer's canonical content.
        MarkdownDocument? currentFile;
        try
        {
            currentFile = await markdownStore.GetAsync(depotId, relativePath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.MarkdownRootUnavailable);
        }
        var document = await repository.GetByPathAsync(depotId, workspace.Id, normalizedPath, cancellationToken);
        var currentHash = currentFile?.ContentHash ?? document?.ContentHash;
        if (document is not null && currentHash is not null &&
            !string.Equals(currentHash, incomingHash, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentHash, command.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentConflict);
        }

        var now = timeProvider.GetUtcNow();
        if (document is not null)
        {
            await repository.MarkIndexPendingAsync(depotId, workspace.Id, normalizedPath, now, cancellationToken);
        }

        if (currentFile is null || !string.Equals(currentHash, incomingHash, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await markdownStore.WriteAtomicAsync(depotId, relativePath, command.Content, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentWriteFailed);
            }
        }

        MarkdownDocument? canonical;
        try
        {
            canonical = await markdownStore.GetAsync(depotId, relativePath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentWriteFailed);
        }

        if (canonical is null)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentWriteFailed);
        }
        var chunks = chunker.Chunk(canonical.Content);
        var write = new DocumentIndexWrite(
            document?.Id ?? idGenerator.NewId(),
            depotId,
            workspace.Id,
            normalizedPath,
            command.Title.Trim(),
            canonical.ContentHash,
            chunks.Select((chunk, ordinal) => new DocumentChunkWrite(idGenerator.NewId(), ordinal, chunk.HeadingPath, chunk.Content, chunk.ContentHash)).ToArray(),
            timeProvider.GetUtcNow());

        var result = await repository.ReconcileIndexAsync(write, cancellationToken);
        return ToModel(result.Document, workspace.Path);
    }

    public async Task<DocumentContentModel?> GetAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(currentDepot.DepotId, documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var workspace = await workspaceAppService.GetAsync(document.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return null;
        }

        var markdown = await markdownStore.GetAsync(currentDepot.DepotId, workspace.Path + "/" + document.Path, cancellationToken);
        if (markdown is null)
        {
            return null;
        }

        var model = ToModel(document, workspace.Path);
        return new DocumentContentModel(model, markdown.Content, markdown.ContentHash);
    }

    public async Task ArchiveAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(currentDepot.DepotId, documentId, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentNotFound);
        var workspace = await workspaceAppService.GetAsync(document.WorkspaceId, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        await using var writeLock = await coordinator.AcquireAsync(currentDepot.DepotId + ":" + workspace.Id + ":" + document.Path, cancellationToken);
        var result = await repository.ArchiveAsync(currentDepot.DepotId, documentId, timeProvider.GetUtcNow(), cancellationToken);
        if (result.Outcome == DocumentArchivePersistenceOutcome.NotFound)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentNotFound);
        }
    }

    private static DocumentModel ToModel(Document document, string workspacePath) => new(
        document.Id,
        document.DepotId,
        document.WorkspaceId,
        workspacePath,
        document.Path,
        document.Title,
        document.ContentHash,
        document.IndexedContentHash,
        document.Status,
        document.IndexStatus,
        document.CreatedAt,
        document.UpdatedAt,
        document.Chunks.OrderBy(x => x.Ordinal).Select(x => new DocumentChunkModel(x.Id, x.Ordinal, x.HeadingPath, x.Content, x.ContentHash)).ToArray());

}
