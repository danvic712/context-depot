using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Documents.Contracts;

/// <summary>
/// Stores canonical Markdown separately from its rebuildable database indexes.
/// </summary>
public interface IMarkdownStore
{
    /// <summary>
    /// Reads a canonical Markdown document.
    /// </summary>
    /// <param name="relativePath">
    /// The canonical path relative to the Markdown root, including the workspace storage path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task whose result contains the Markdown path, content and hash, or null when the file is missing.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// The canonical path is invalid.
    /// </exception>
    /// <exception cref="IOException">
    /// The file cannot be read.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The process lacks permission to read the file.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    Task<MarkdownDocument?> GetAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically replaces canonical content without exposing a partially written destination file.
    /// </summary>
    /// <param name="relativePath">
    /// The canonical path relative to the Markdown root, including the workspace storage path.
    /// </param>
    /// <param name="content">
    /// The complete Markdown content to persist.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that completes after the canonical file is replaced.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// The canonical path is invalid.
    /// </exception>
    /// <exception cref="IOException">
    /// The temporary file cannot be written or moved to the canonical path.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The process lacks permission to create or replace the file.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    Task WriteAtomicAsync(string relativePath, string content, CancellationToken cancellationToken);

    /// <summary>
    /// Removes canonical content, treating an already missing file as deleted.
    /// </summary>
    /// <param name="relativePath">
    /// The canonical path relative to the Markdown root, including the workspace storage path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels processing before the file is removed.
    /// </param>
    /// <returns>
    /// A task that completes after the file is removed or confirmed absent.
    /// </returns>
    /// <exception cref="ContextDepotApplicationException">
    /// The canonical path is invalid.
    /// </exception>
    /// <exception cref="IOException">
    /// The file cannot be deleted.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The process lacks permission to delete the file.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation was requested before the file was removed.
    /// </exception>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the canonical Markdown root supports file creation, read/write access and cleanup.
    /// </summary>
    /// <returns>
    /// True when a temporary readiness probe succeeds; false when an I/O or access-permission failure prevents the probe.
    /// </returns>
    /// <remarks>
    /// The probe may create the configured root directory and removes its temporary file on close.
    /// </remarks>
    bool CanReadAndWrite();
}
