namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record DocumentIndexRepairCandidate(
    Guid DocumentId,
    Guid OwnerId,
    Guid WorkspaceId,
    string WorkspacePath,
    string Path,
    string Title);
