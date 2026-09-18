using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Persistence;
using ContextDepot.Application.Safety;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Documents;

public sealed class DocumentAppService(
    ICurrentOwnerContext currentOwner,
    IWorkspaceAppService workspaceAppService,
    IDocumentRepository repository,
    IMarkdownStore markdownStore,
    IMarkdownChunker chunker,
    DocumentWriteCoordinator coordinator,
    ISourceSafetyService sourceSafety,
    IIdGenerator idGenerator,
    IClock clock) : IDocumentAppService
{
    public async Task<DocumentModel> UpsertAsync(UpsertDocumentCommand command, CancellationToken cancellationToken)
    {
        var workspace = await workspaceAppService.ResolveAsync(command.Workspace, cancellationToken)
            ?? throw new ContextDepotApplicationException("WorkspaceNotFound", "The requested workspace does not exist.");
        var normalizedPath = DocumentPath.Normalize(command.Path);
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new ContextDepotApplicationException("InvalidDocumentTitle", "Document title is required.");
        }

        sourceSafety.EnsureSafe(command.Content);
        sourceSafety.EnsureSafe(command.Title);
        var relativePath = workspace.Path + "/" + normalizedPath;
        var incomingHash = Hash(command.Content);
        await using var writeLock = await coordinator.AcquireAsync(workspace.Id + ":" + normalizedPath, cancellationToken);

        var currentFile = await markdownStore.GetAsync(relativePath, cancellationToken);
        var currentHash = currentFile?.ContentHash;
        var document = await repository.GetByPathAsync(currentOwner.OwnerId, workspace.Id, normalizedPath, cancellationToken);
        if (document is not null && currentHash is null)
        {
            currentHash = document.ContentHash;
        }

        if (document is not null && !string.Equals(currentHash, incomingHash, StringComparison.OrdinalIgnoreCase) && !string.Equals(currentHash, command.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ContextDepotApplicationException("DocumentConflict", "The canonical Markdown changed since it was read.");
        }

        if (document is null)
        {
            document = new Document(idGenerator.NewId(), currentOwner.OwnerId, workspace.Id, normalizedPath, command.Title.Trim(), clock.UtcNow);
            await repository.AddAsync(document, cancellationToken);
        }

        document.MarkPending(clock.UtcNow);
        repository.Update(document);
        await repository.SaveChangesAsync(cancellationToken);

        if (currentFile is null || !string.Equals(currentHash, incomingHash, StringComparison.OrdinalIgnoreCase))
        {
            await markdownStore.WriteAtomicAsync(relativePath, command.Content, cancellationToken);
        }

        var canonical = await markdownStore.GetAsync(relativePath, cancellationToken)
            ?? throw new ContextDepotApplicationException("MarkdownWriteFailed", "The canonical Markdown file could not be read after writing.");
        var chunks = chunker.Chunk(canonical.Content);
        repository.RemoveChunks(document);
        for (var ordinal = 0; ordinal < chunks.Count; ordinal++)
        {
            var chunk = chunks[ordinal];
            repository.AddChunk(new DocumentChunk(idGenerator.NewId(), currentOwner.OwnerId, document.Id, workspace.Id, ordinal, chunk.HeadingPath, chunk.Content, chunk.ContentHash, clock.UtcNow));
        }

        document.UpdateMetadata(command.Title.Trim(), canonical.ContentHash, DocumentIndexStatus.Indexed, clock.UtcNow);
        repository.Update(document);
        await repository.SaveChangesAsync(cancellationToken);
        return ToModel(document, workspace.Path);
    }

    public async Task<DocumentContentModel?> GetAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(currentOwner.OwnerId, documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var workspace = await workspaceAppService.GetAsync(document.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return null;
        }

        var markdown = await markdownStore.GetAsync(workspace.Path + "/" + document.Path, cancellationToken);
        if (markdown is null)
        {
            return null;
        }

        var model = ToModel(document, workspace.Path);
        return new DocumentContentModel(model, markdown.Content, markdown.ContentHash);
    }

    public async Task ArchiveAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(currentOwner.OwnerId, documentId, cancellationToken)
            ?? throw new ContextDepotApplicationException("DocumentNotFound", "The requested document does not exist.");
        await using var writeLock = await coordinator.AcquireAsync(document.WorkspaceId + ":" + document.Path, cancellationToken);
        document.MarkArchived(clock.UtcNow);
        repository.Update(document);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static DocumentModel ToModel(Document document, string workspacePath) => new(
        document.Id,
        document.OwnerId,
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

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
