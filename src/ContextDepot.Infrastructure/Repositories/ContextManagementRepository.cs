using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Repositories;

/// <summary>
/// Deletes contexts and every stored vector profile within a single PostgreSQL transaction.
/// </summary>
/// <param name="db">
/// The database context that owns the deletion transaction.
/// </param>
/// <param name="options">
/// The vector-store schema configuration.
/// </param>
public sealed class ContextManagementRepository(ContextDepotDbContext db, IOptions<PostgreSqlVectorStoreOptions> options) : IContextManagementRepository
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
    public async Task<bool> DeleteAsync(Guid depotId, Guid contextId, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.ContextItems.AnyAsync(x => x.DepotId == depotId && x.Id == contextId, cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextNotFound);
        // Preserve successor records while removing their reference to the deleted history item.
        await db.ContextItems.Where(x => x.DepotId == depotId && x.SupersedesId == contextId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SupersedesId, (Guid?)null), cancellationToken);
        var deleted = await db.ContextItems.Where(x => x.DepotId == depotId && x.Id == contextId && x.UpdatedAt == expectedUpdatedAt)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0) return false;
        await KnowledgeVectorDeletion.DeleteContextAsync(db, options.Value.Schema, depotId, contextId, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
