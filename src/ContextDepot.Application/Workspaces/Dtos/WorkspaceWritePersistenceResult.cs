namespace ContextDepot.Application.Workspaces.Dtos;

// Internal snapshots are for workspace writes.
public sealed record WorkspaceWritePersistenceResult(bool Applied, long AccessVersion);
