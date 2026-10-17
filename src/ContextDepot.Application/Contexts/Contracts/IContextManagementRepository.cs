using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Contexts.Contracts;

/// <summary>
/// Persists permanent context deletion within the trusted Web management scope.
/// </summary>
public interface IContextManagementRepository
{
    /// <summary>
    /// Deletes the observed context revision and every vector profile in one database transaction.
    /// </summary>
    /// <param name="depotId">
    /// The server-selected depot that owns the context.
    /// </param>
    /// <param name="contextId">
    /// The context identifier.
    /// </param>
    /// <param name="expectedUpdatedAt">
    /// The exact revision returned by the reader.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the transaction before commit.
    /// </param>
    /// <returns>
    /// A task whose result is true after deletion commits, or false when the observed revision no longer matches.
    /// </returns>
    /// <remarks>
    /// Successor contexts are preserved while their reference to the deleted context is cleared.
    /// A revision mismatch rolls back all changes, including successor-reference updates.
    /// </remarks>
    /// <exception cref="ContextDepotApplicationException">
    /// The context does not exist in the selected depot.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    Task<bool> DeleteAsync(Guid depotId, Guid contextId, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken);
}
