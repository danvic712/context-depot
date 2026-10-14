using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Application.Shared.Dtos;

namespace ContextDepot.Application.Workspaces;

public sealed class WorkspaceBrowserAppService(
    ICurrentDepotContext currentDepot,
    IWorkspaceBrowserRepository repository,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Returns a filtered, ordered directory within the current depot and workspace authorization scope.
    /// </summary>
    /// <param name="query">
    /// The requested scope, conditions and pagination.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the request.
    /// </param>
    /// <returns>
    /// The requested directory page and its filtered total count.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// The query is invalid or the parent is outside the visible scope.
    /// </exception>
    public async Task<WorkspaceDirectory> BrowseAsync(WorkspaceDirectoryQuery query, CancellationToken cancellationToken)
    {
        query = query with { Query = query.Query?.Trim() };
        if (!query.IsValid() || query.Query?.Length > WorkspaceDirectoryQuery.MaximumQueryLength ||
            !Enum.IsDefined(query.Status) || !Enum.IsDefined(query.Content) || !Enum.IsDefined(query.Sort) ||
            (query.UpdatedWithinDays is { } window && !Enum.IsDefined(window)) ||
            (query.UpdatedWithinDays is not null && (query.UpdatedFrom is not null || query.UpdatedBefore is not null)) ||
            (query.UpdatedFrom is not null && query.UpdatedBefore is not null && query.UpdatedFrom >= query.UpdatedBefore) ||
            (query.Status == WorkspaceContentStatus.Empty && query.Content != WorkspaceContentType.All))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        var now = timeProvider.GetUtcNow();
        if (query.ParentId is Guid id && await repository.GetAsync(currentDepot.DepotId, id, now, cancellationToken) is null)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        return await repository.BrowseAsync(currentDepot.DepotId, query, now, cancellationToken);
    }

    /// <summary>
    /// Reads one workspace within the current visible scope.
    /// </summary>
    /// <param name="id">
    /// The workspace identifier.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the request.
    /// </param>
    /// <returns>
    /// The workspace detail.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// The workspace is outside the visible scope.
    /// </exception>
    public async Task<WorkspaceDetail> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(currentDepot.DepotId, id, timeProvider.GetUtcNow(), cancellationToken)
        ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);

    /// <summary>
    /// Reads valid contexts and active documents saved directly to one workspace.
    /// </summary>
    /// <param name="id">
    /// The workspace identifier.
    /// </param>
    /// <param name="query">
    /// The pagination request.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the request.
    /// </param>
    /// <returns>
    /// The requested knowledge page.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// Pagination is invalid or the workspace is outside the visible scope.
    /// </exception>
    public async Task<WorkspaceKnowledge> ListKnowledgeAsync(Guid id, PaginationQuery query, CancellationToken cancellationToken)
    {
        if (!query.IsValid())
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        return await repository.ListKnowledgeAsync(currentDepot.DepotId, id, query,
            timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
    }
}
