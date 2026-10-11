using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;

namespace ContextDepot.Application.AgentContext.Dtos;

public sealed record AgentWorkspaceResolution(WorkspaceSubject? Subject, WorkspaceSubjectStatus Status,
    WorkspaceModel? Target, IReadOnlyList<WorkspaceModel> Candidates, bool? CanCreateRootWorkspaces);
