using ContextDepot.Application.Workspaces.Enums;

namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceSubjectResolution(WorkspaceSubject Subject, WorkspaceSubjectStatus Status,
    WorkspaceModel? Target, bool CanCreateRootWorkspaces);
