using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Contracts;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid depotId, Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspacePathLookup?> GetByIdWithPathAsync(Guid depotId, Guid workspaceId, CancellationToken cancellationToken);

    Task<Workspace?> GetByPathAsync(Guid depotId, string normalizedPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspacePathLookup>> ListWithPathsAsync(Guid depotId, string? parentPath, CancellationToken cancellationToken);

    Task<WorkspaceUpsertPersistenceResult> UpsertPathAsync(
        Guid depotId,
        string normalizedPath,
        string name,
        string? description,
        string metadataJson,
        bool createMissingParents,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
