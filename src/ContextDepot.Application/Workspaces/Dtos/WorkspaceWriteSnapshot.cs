using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Dtos;

// Internal snapshots are for workspace writes.
public sealed record WorkspaceWriteSnapshot(WorkspaceSubject? Subject, WorkspaceSubjectBinding? Binding,
    IReadOnlyList<WorkspacePathLookup> Workspaces, WorkspaceAccessSnapshot Access);
