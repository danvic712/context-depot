using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Documents.Contracts;

/// <summary>
/// Lists archived documents and coordinates permanent removal of metadata, derived indexes and canonical Markdown.
/// </summary>
public interface IDocumentManagementRepository
{
    /// <summary>
    /// Reads an archive page and its total count from a consistent database snapshot.
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
    Task<ArchivedDocumentPage> ListArchivedAsync(Guid depotId, Guid workspaceId, PaginationQuery pagination, CancellationToken cancellationToken);

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
    Task DeleteAsync(Guid depotId, Guid documentId, Guid workspaceId, string relativePath,
        DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken);
}
