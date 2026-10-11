using ContextDepot.Application.Workspaces.Enums;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceWriteResult(WorkspaceModel Workspace, EnsureWorkspaceOutcome? Outcome = null);
