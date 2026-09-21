namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record DocumentIndexRepairCandidate(
    Guid DocumentId,
    Guid DepotId,
    Guid WorkspaceId,
    string WorkspacePath,
    string Path,
    string Title);
