namespace ContextDepot.Application.Setup.Dtos;

public sealed record SetupWorkspaceDto(Guid Id, string Name, string Path, string? Description);
