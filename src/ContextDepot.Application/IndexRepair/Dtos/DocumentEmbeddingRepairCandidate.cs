namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record DocumentEmbeddingRepairCandidate(
    Guid DocumentChunkId,
    Guid DocumentId,
    Guid DepotId,
    Guid WorkspaceId,
    string WorkspacePath,
    string DocumentPath,
    string Title,
    string HeadingPath,
    string Content);
