namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapWorkspaceCandidate(
    Guid Id,
    Guid OwnerId,
    Guid? ParentWorkspaceId,
    string Name,
    string Slug);
