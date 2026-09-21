namespace ContextDepot.Application.Workspaces.Dtos;

public sealed record WorkspaceModel(
    Guid Id,
    Guid DepotId,
    string Path,
    string Name,
    string? Description,
    string MetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
