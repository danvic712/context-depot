using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Dtos;

// Same-depot internal snapshots are never returned as public resolution candidates.
public sealed record WorkspaceWriteSnapshot(WorkspaceSubject? Subject, WorkspaceSubjectBinding? Binding,
    IReadOnlyList<WorkspacePathLookup> Workspaces, WorkspaceAccessSnapshot Access);
