namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapWorkspaceCandidate(
    Guid Id,
    Guid DepotId,
    Guid? ParentWorkspaceId,
    string Name,
    string Slug);
