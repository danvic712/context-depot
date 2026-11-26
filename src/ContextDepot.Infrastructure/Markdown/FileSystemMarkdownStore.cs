using System.Text;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Markdown;

/// <summary>
/// Stores canonical Markdown as UTF-8 files under the configured Markdown root.
/// </summary>
/// <param name="environment">
/// The host environment used to resolve a relative Markdown root.
/// </param>
/// <param name="options">
/// The configured canonical Markdown root.
/// </param>
/// <exception cref="ContextDepotApplicationException">
/// The configured Markdown root is blank.
/// </exception>
public sealed class FileSystemMarkdownStore(IHostEnvironment environment, IOptions<MarkdownStoreOptions> options) : IMarkdownStore
{
    private readonly string root = ResolveRoot(environment.ContentRootPath, options.Value.Root);

    /// <summary>
    /// Reads a canonical Markdown document.
    /// </summary>
    /// <param name="relativePath">
    /// The canonical path relative to the configured Markdown root, including the workspace storage path.
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
    public async Task<MarkdownDocument?> GetAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return new MarkdownDocument(relativePath, content, DocumentContentHasher.Compute(content));
    }

    /// <summary>
    /// Atomically replaces canonical content without exposing a partially written destination file.
    /// </summary>
    /// <param name="relativePath">
    /// The canonical path relative to the configured Markdown root, including the workspace storage path.
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
    public async Task WriteAtomicAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Path.GetRandomFileName() + ".tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
            {
                await writer.WriteAsync(content.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Removes canonical content, treating an already missing file as deleted.
    /// </summary>
    /// <param name="relativePath">
    /// The canonical path relative to the configured Markdown root, including the workspace storage path.
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
    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether the canonical Markdown root supports file creation, read/write access and cleanup.
    /// </summary>
    /// <returns>
    /// True when a temporary readiness probe succeeds; false when an I/O or access-permission failure prevents the probe.
    /// </returns>
    /// <remarks>
    /// The probe may create the configured root directory and removes its temporary file on close.
    /// </remarks>
    public bool CanReadAndWrite()
    {
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, ".context-depot-ready-" + Path.GetRandomFileName());
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.ReadWrite,
                       FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        if (normalized.Length == 0 || Path.IsPathRooted(relativePath) || normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is "." or ".." or ""))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        return full;
    }

    private static string ResolveRoot(string contentRoot, string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.MarkdownRootUnavailable);
        }

        var root = Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(contentRoot, configuredRoot);
        return Path.GetFullPath(root);
    }

}
