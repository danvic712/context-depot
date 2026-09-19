namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record DocumentEmbeddingRepairCandidate(
    Guid DocumentChunkId,
    Guid DocumentId,
    Guid OwnerId,
    Guid WorkspaceId,
    string WorkspacePath,
    string DocumentPath,
    string Title,
    string HeadingPath,
    string Content);
