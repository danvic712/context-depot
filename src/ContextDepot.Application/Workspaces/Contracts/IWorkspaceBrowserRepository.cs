using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Shared.Dtos;

namespace ContextDepot.Application.Workspaces.Contracts;

/// <summary>
/// Reads authorized workspace directories and knowledge without expanding knowledge into child spaces.
/// </summary>
public interface IWorkspaceBrowserRepository
{
    /// <summary>
    /// Filters and counts authorized spaces before applying ordering and pagination.
    /// </summary>
    /// <param name="depotId">
    /// The current depot boundary.
    /// </param>
    /// <param name="query">
    /// The complete, validated directory request.
    /// </param>
    /// <param name="asOf">
    /// The timestamp used consistently for knowledge validity and rolling windows.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels database operations.
    /// </param>
    /// <returns>
    /// A directory page and its filtered total count.
    /// </returns>
    Task<WorkspaceDirectory> BrowseAsync(Guid depotId, WorkspaceDirectoryQuery query,
        DateTimeOffset asOf, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one authorized workspace and its navigable ancestors.
    /// </summary>
    /// <param name="depotId">
    /// The current depot boundary.
    /// </param>
    /// <param name="id">
    /// The workspace identifier.
    /// </param>
    /// <param name="asOf">
    /// The timestamp used to count valid knowledge.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels database operations.
    /// </param>
    /// <returns>
    /// The detail, or null when the workspace is outside the visible scope.
    /// </returns>
    Task<WorkspaceDetail?> GetAsync(Guid depotId, Guid id, DateTimeOffset asOf, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a page of visible knowledge belonging directly to a workspace.
    /// </summary>
    /// <param name="depotId">
    /// The current depot boundary.
    /// </param>
    /// <param name="id">
    /// The workspace identifier.
    /// </param>
    /// <param name="query">
    /// The pagination request.
    /// </param>
    /// <param name="asOf">
    /// The timestamp used to filter valid contexts.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels database operations.
    /// </param>
    /// <returns>
    /// A knowledge page, or null when the workspace is outside the visible scope.
    /// </returns>
    Task<WorkspaceKnowledge?> ListKnowledgeAsync(Guid depotId, Guid id, PaginationQuery query,
        DateTimeOffset asOf, CancellationToken cancellationToken);
}
