using ContextDepot.Application.Documents.Dtos;

namespace ContextDepot.Application.Documents.Contracts;

public interface IMarkdownStore
{
    Task<MarkdownDocument?> GetAsync(string relativePath, CancellationToken cancellationToken);

    Task WriteAtomicAsync(string relativePath, string content, CancellationToken cancellationToken);

    bool CanReadAndWrite();
}
