namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record UpsertWorkspaceCommand(
    string Path,
    string Name,
    string? Description = null,
    string MetadataJson = "{}",
    bool CreateMissingParents = false);
