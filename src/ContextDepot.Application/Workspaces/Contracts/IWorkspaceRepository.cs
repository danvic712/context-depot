using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Workspaces.Contracts;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspacePathLookup?> GetByIdWithPathAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<Workspace?> GetByPathAsync(string normalizedPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspacePathLookup>> ListWithPathsAsync(string? parentPath, CancellationToken cancellationToken);

    /// <summary>Loads every readable workspace with its resolved path.</summary>
    Task<IReadOnlyList<WorkspacePathLookup>> ListVisibleWithPathsAsync(CancellationToken cancellationToken);

    Task<WorkspaceTopology> LoadVisibleTopologyAsync(CancellationToken cancellationToken);

    Task<WorkspaceWriteSnapshot> LoadEnsureSnapshotAsync(WorkspaceSubject? subject,
        CancellationToken cancellationToken);

    Task<WorkspaceWritePersistenceResult> TryApplyEnsureAsync(WorkspaceWritePlan plan,
        DateTimeOffset now, CancellationToken cancellationToken);

    Task<WorkspaceUpsertPersistenceResult> UpsertPathAsync(
        string normalizedPath,
        string name,
        string? description,
        string metadataJson,
        bool createMissingParents,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
