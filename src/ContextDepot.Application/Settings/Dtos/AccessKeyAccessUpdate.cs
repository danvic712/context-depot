namespace ContextDepot.Application.Settings.Dtos;

public sealed record AccessKeyAccessUpdate(IReadOnlyList<Guid> WorkspaceIds, bool? CanCreateRootWorkspaces,
    long? ExpectedAccessVersion);
