using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Contracts;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspacePathLookup?> GetByIdWithPathAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken);

    Task<Workspace?> GetByPathAsync(Guid ownerId, string normalizedPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspacePathLookup>> ListWithPathsAsync(Guid ownerId, string? parentPath, CancellationToken cancellationToken);

    Task<WorkspaceUpsertPersistenceResult> UpsertPathAsync(
        Guid ownerId,
        string normalizedPath,
        string name,
        string? description,
        string metadataJson,
        bool createMissingParents,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
