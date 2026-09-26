using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;

namespace ContextDepot.Application.Documents;

public sealed class CanonicalDocumentIndexer(
    IDocumentRepository repository,
    IMarkdownStore markdownStore,
    HeadingAwareMarkdownChunker chunker,
    ISourceSafetyService sourceSafety,
    IIdGenerator idGenerator,
    TimeProvider timeProvider)
{
    public Task<MarkdownDocument?> ReadAsync(
        Guid depotId,
        string workspacePath,
        string documentPath,
        CancellationToken cancellationToken) =>
        markdownStore.GetAsync(depotId, workspacePath + "/" + documentPath, cancellationToken);

    public Task<DocumentReconcilePersistenceResult> ReconcileAsync(
        CanonicalDocumentSource source,
        CancellationToken cancellationToken)
    {
        sourceSafety.EnsureSafe(source.WorkspacePath);
        sourceSafety.EnsureSafe(source.DocumentPath);
        sourceSafety.EnsureSafe(source.Title);
        sourceSafety.EnsureSafe(source.Markdown.Content);

        var chunks = chunker.Chunk(source.Markdown.Content)
            .Select((chunk, ordinal) => new DocumentChunkWrite(
                idGenerator.NewId(), ordinal, chunk.HeadingPath, chunk.Content, chunk.ContentHash))
            .ToArray();
        var write = new DocumentIndexWrite(
            source.DocumentId,
            source.DepotId,
            source.WorkspaceId,
            source.DocumentPath,
            source.Title,
            source.Markdown.ContentHash,
            chunks,
            timeProvider.GetUtcNow());
        return repository.ReconcileIndexAsync(write, cancellationToken);
    }
}

public sealed record CanonicalDocumentSource(
    Guid DocumentId,
    Guid DepotId,
    Guid WorkspaceId,
    string WorkspacePath,
    string DocumentPath,
    string Title,
    MarkdownDocument Markdown);
