namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record EnsureWorkspaceCommand(WorkspaceSubject Subject, string Path, string Name,
    string? Description = null, string MetadataJson = "{}");
