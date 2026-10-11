namespace ContextDepot.Application.Settings.Dtos;

public sealed record CreateAccessKeyRequest(string Name, Guid[] WorkspaceIds, bool CanCreateRootWorkspaces = false);
