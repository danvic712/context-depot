namespace ContextDepot.Application.Setup.Dtos;

public sealed record SetupWorkspaceRequest(string Name, string Path, string? Description = null);
