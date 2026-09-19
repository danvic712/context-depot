namespace ContextDepot.Application.Documents.Dtos;

public sealed record DocumentIndexWrite(
    Guid DocumentId,
    Guid OwnerId,
    Guid WorkspaceId,
    string Path,
    string Title,
    string ContentHash,
    IReadOnlyList<DocumentChunkWrite> Chunks,
    DateTimeOffset Now);
