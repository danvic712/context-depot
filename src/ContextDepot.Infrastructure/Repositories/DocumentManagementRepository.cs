using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Repositories;

/// <summary>
/// Lists archived documents and removes document sources together with their derived indexes.
/// </summary>
/// <param name="db">
/// The database context used for archive snapshots and deletion transactions.
/// </param>
/// <param name="markdown">
/// The canonical Markdown store used for deletion and commit-failure compensation.
/// </param>
/// <param name="vectorOptions">
/// The vector-store schema configuration.
/// </param>
public sealed class DocumentManagementRepository(ContextDepotDbContext db, IMarkdownStore markdown,
    IOptions<PostgreSqlVectorStoreOptions> vectorOptions) : IDocumentManagementRepository
{
    /// <summary>
    /// Reads an archive page and its total count from a repeatable-read snapshot.
    /// </summary>
    /// <param name="depotId">
    /// The server-selected depot.
    /// </param>
    /// <param name="workspaceId">
    /// The workspace already authorized by the application service.
    /// </param>
    /// <param name="pagination">
    /// Validated, one-based pagination bounds.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains archived documents directly owned by the workspace and the total from the same database snapshot.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public async Task<ArchivedDocumentPage> ListArchivedAsync(Guid depotId, Guid workspaceId, PaginationQuery pagination, CancellationToken cancellationToken)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var query = db.Documents.AsNoTracking().Where(x => x.DepotId == depotId && x.WorkspaceId == workspaceId && x.Status == DocumentStatus.Archived);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((pagination.Page - 1) * pagination.PageSize).Take(pagination.PageSize)
            .Select(x => new ArchivedDocument(x.Id, x.Title, x.Path, x.UpdatedAt)).ToArrayAsync(cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        return new(items, total, pagination.Page, pagination.PageSize);
    }

    /// <summary>
    /// Removes an active or archived document only if its observed revision still matches.
    /// </summary>
    /// <param name="depotId">
    /// The server-selected depot.
    /// </param>
    /// <param name="documentId">
    /// The active or archived document identifier.
    /// </param>
    /// <param name="workspaceId">
    /// The document's server-resolved workspace.
    /// </param>
    /// <param name="relativePath">
    /// The server-resolved canonical path, including the workspace path.
    /// </param>
    /// <param name="expectedUpdatedAt">
    /// The exact revision returned by the reader.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels processing before canonical Markdown is removed.
    /// </param>
    /// <returns>
    /// A task that completes after metadata, chunks, every vector profile and canonical Markdown are removed.
    /// </returns>
    /// <remarks>
    /// The caller must hold the document write lease. Database changes use one transaction.
    /// After canonical Markdown is removed, commit proceeds without client cancellation. If commit fails,
    /// any previously read canonical content is restored before the failure is propagated.
    /// </remarks>
    /// <exception cref="ContextDepotApplicationException">
    /// The document identity, workspace or observed revision no longer matches, or the canonical path is invalid.
    /// </exception>
    /// <exception cref="IOException">
    /// Canonical content cannot be read, deleted or restored.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The process cannot access the canonical file.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before canonical Markdown was removed.
    /// </exception>
    public async Task DeleteAsync(Guid depotId, Guid documentId, Guid workspaceId, string relativePath,
        DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        MarkdownDocument? original = null;
        var sourceDeleted = false;
        try
        {
            var removed = await db.Documents.Where(x => x.DepotId == depotId && x.Id == documentId &&
                x.WorkspaceId == workspaceId && x.UpdatedAt == expectedUpdatedAt)
                .ExecuteDeleteAsync(cancellationToken);
            if (removed == 0) throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentConflict);
            // The document deletion cascades to its chunks. Clear every embedding profile,
            // including profiles that are no longer configured, in this same transaction.
            await KnowledgeVectorDeletion.DeleteDocumentAsync(db, vectorOptions.Value.Schema, depotId, documentId, transaction, cancellationToken);
            original = await markdown.GetAsync(depotId, relativePath, cancellationToken);
            await markdown.DeleteAsync(depotId, relativePath, cancellationToken);
            sourceDeleted = true;
            // Once the source is removed, finish the commit even if the client disconnects.
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            // Provider disposal rolls back only when a transaction is still pending.
            // A rejected COMMIT may already have rolled back on the PostgreSQL side.
            await transaction.DisposeAsync();
            if (sourceDeleted && original is not null)
                await markdown.WriteAtomicAsync(depotId, relativePath, original.Content, CancellationToken.None);
            throw;
        }
    }

}
