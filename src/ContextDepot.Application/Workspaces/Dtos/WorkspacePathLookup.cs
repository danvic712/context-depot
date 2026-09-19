using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspacePathLookup(Workspace Workspace, string Path);
