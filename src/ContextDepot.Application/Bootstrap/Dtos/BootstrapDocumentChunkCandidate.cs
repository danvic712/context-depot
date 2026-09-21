namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapDocumentChunkCandidate(
    Guid Id,
    Guid DepotId,
    Guid DocumentId,
    Guid WorkspaceId,
    int Ordinal,
    string Path,
    string Title,
    string HeadingPath,
    string Content,
    string ContentHash,
    DateTimeOffset UpdatedAt);
