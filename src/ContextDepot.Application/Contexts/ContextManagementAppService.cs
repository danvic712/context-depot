using ContextDepot.Application.Shared.Runtime;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Contexts;

/// <summary>
/// Validates trusted management access and optimistic concurrency before permanent context removal.
/// </summary>
/// <param name="depot">
/// The server-selected depot context.
/// </param>
/// <param name="access">
/// The current workspace access scope.
/// </param>
/// <param name="repository">
/// The repository that atomically deletes the context and its vector indexes.
/// </param>
public sealed class ContextManagementAppService(ICurrentDepotContext depot, IWorkspaceAccessContext access,
    IContextManagementRepository repository)
{
    /// <summary>
    /// Deletes a context and its derived indexes only if the observed revision is still current.
    /// </summary>
    /// <param name="id">
    /// The context identifier within the server-selected depot.
    /// </param>
    /// <param name="expectedUpdatedAt">
    /// The exact revision obtained from the reader.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that completes after the context deletion commits.
    /// </returns>
    /// <remarks>
    /// Successor contexts are preserved; their reference to the deleted context is cleared in the same transaction.
    /// </remarks>
    /// <exception cref="ContextDepotApplicationException">
    /// Management access is denied, the revision is missing, the context does not exist, or its revision has changed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task DeleteAsync(Guid id, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        ManagementAccess.RequireManagement(depot, access);
        if (expectedUpdatedAt == default) throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidResourceQuery);
        if (!await repository.DeleteAsync(depot.DepotId, id, expectedUpdatedAt, cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.KnowledgeDeleteConflict);
    }
}
