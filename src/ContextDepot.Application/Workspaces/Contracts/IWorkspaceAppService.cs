using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces;

namespace ContextDepot.Application.Workspaces.Contracts;

public interface IWorkspaceAppService
{
    Task<WorkspaceModel?> GetAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspaceModel?> ResolveAsync(string path, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceModel>> ListAsync(string? parentPath, CancellationToken cancellationToken);

    /// <summary>Lists all readable workspaces, including nested scopes, with their names and paths.</summary>
    Task<IReadOnlyList<WorkspaceModel>> ListVisibleAsync(CancellationToken cancellationToken);

    Task<WorkspaceTopology> LoadTopologyAsync(CancellationToken cancellationToken);

    Task<WorkspaceModel> UpsertAsync(UpsertWorkspaceCommand command, CancellationToken cancellationToken);

    Task<WorkspaceSubjectResolution> ResolveSubjectAsync(WorkspaceSubject subject, CancellationToken cancellationToken);

    Task<WorkspaceWriteResult> EnsureAsync(EnsureWorkspaceCommand command, CancellationToken cancellationToken);
}
