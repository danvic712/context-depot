using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces;

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
