namespace ContextDepot.Application.Documents;

public sealed record MarkdownDocument(string Path, string Content, string ContentHash);

public interface IMarkdownStore
{
    Task<MarkdownDocument?> GetAsync(string relativePath, CancellationToken cancellationToken);

    Task WriteAtomicAsync(string relativePath, string content, CancellationToken cancellationToken);

    bool CanReadAndWrite();
}
