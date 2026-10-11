namespace ContextDepot.Application.Settings.Dtos;

public sealed record UpdateAccessKeyGrantsRequest(Guid[] WorkspaceIds, bool? CanCreateRootWorkspaces = null,
    string? ExpectedAccessVersion = null);
