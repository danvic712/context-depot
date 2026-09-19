namespace ContextDepot.Application.Workspaces.Enums;

public enum WorkspaceUpsertPersistenceOutcome
{
    Created,
    Updated,
    ReusedExisting,
    ParentNotFound,
    ConcurrencyConflict
}
