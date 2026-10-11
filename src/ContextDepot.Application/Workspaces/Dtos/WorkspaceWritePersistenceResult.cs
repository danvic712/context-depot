namespace ContextDepot.Application.Workspaces.Dtos;

// Same-depot internal snapshots are never returned as public resolution candidates.
public sealed record WorkspaceWritePersistenceResult(bool Applied, long AccessVersion);
