using System.Text;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Markdown;

public sealed class FileSystemMarkdownStore(IHostEnvironment environment, IOptions<MarkdownStoreOptions> options) : IMarkdownStore
{
    private readonly string root = ResolveRoot(environment.ContentRootPath, options.Value.Root);

    public async Task<MarkdownDocument?> GetAsync(Guid depotId, string relativePath, CancellationToken cancellationToken)
    {
        var path = ResolvePath(depotId, relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return new MarkdownDocument(relativePath, content, DocumentContentHasher.Compute(content));
    }

    public async Task WriteAtomicAsync(Guid depotId, string relativePath, string content, CancellationToken cancellationToken)
    {
        var path = ResolvePath(depotId, relativePath);
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

    private string ResolvePath(Guid depotId, string relativePath)
    {
        if (depotId == Guid.Empty)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        var normalized = relativePath.Replace('\\', '/').Trim();
        if (normalized.Length == 0 || Path.IsPathRooted(relativePath) || normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is "." or ".." or ""))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidDocumentPath);
        }

        // The database is scoped by DepotId, so the physical source of truth must be
        // scoped by the same boundary. Never put two depots directly under the same
        // Markdown root: identical workspace/document paths would otherwise alias.
        var depotRoot = Path.Combine(root, depotId.ToString("N"));
        var full = Path.GetFullPath(Path.Combine(depotRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = depotRoot.EndsWith(Path.DirectorySeparatorChar) ? depotRoot : depotRoot + Path.DirectorySeparatorChar;
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
