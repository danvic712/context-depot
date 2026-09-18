using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Workspaces;

public sealed record WorkspaceModel(
    Guid Id,
    Guid OwnerId,
    string Path,
    string Name,
    string? Description,
    string MetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpsertWorkspaceCommand(
    string Path,
    string Name,
    string? Description = null,
    string MetadataJson = "{}",
    bool CreateMissingParents = false);

public interface IWorkspaceAppService
{
    Task<WorkspaceModel?> GetAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspaceModel?> ResolveAsync(string path, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceModel>> ListAsync(string? parentPath, CancellationToken cancellationToken);

    Task<WorkspaceModel> UpsertAsync(UpsertWorkspaceCommand command, CancellationToken cancellationToken);
}

internal static class WorkspaceModelMapper
{
    public static WorkspaceModel ToModel(Workspace entity, string path) => new(
        entity.Id,
        entity.OwnerId,
        path,
        entity.Name,
        entity.Description,
        entity.MetadataJson,
        entity.CreatedAt,
        entity.UpdatedAt);
}
