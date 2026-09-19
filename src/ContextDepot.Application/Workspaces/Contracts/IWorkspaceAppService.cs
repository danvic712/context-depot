using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.Workspaces.Contracts;

public interface IWorkspaceAppService
{
    Task<WorkspaceModel?> GetAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspaceModel?> ResolveAsync(string path, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceModel>> ListAsync(string? parentPath, CancellationToken cancellationToken);

    Task<WorkspaceModel> UpsertAsync(UpsertWorkspaceCommand command, CancellationToken cancellationToken);
}
