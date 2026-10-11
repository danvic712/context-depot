using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Dtos;

// Same-depot internal snapshots are never returned as public resolution candidates.
public sealed record WorkspaceWritePlan(WorkspaceWriteSnapshot Expected, WorkspacePathLookup Target,
    IReadOnlyList<Workspace> Created, bool UpdateTarget, WorkspaceSubjectBinding? NewBinding)
{
    public WorkspaceUpsertPersistenceOutcome Outcome => Created.Count > 0 ? WorkspaceUpsertPersistenceOutcome.Created :
        UpdateTarget ? WorkspaceUpsertPersistenceOutcome.Updated : WorkspaceUpsertPersistenceOutcome.ReusedExisting;
}
