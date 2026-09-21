using ContextDepot.Application.Documents.Dtos;

namespace ContextDepot.Application.Documents.Contracts;

public interface IMarkdownStore
{
    Task<MarkdownDocument?> GetAsync(Guid depotId, string relativePath, CancellationToken cancellationToken);

    Task WriteAtomicAsync(Guid depotId, string relativePath, string content, CancellationToken cancellationToken);

    bool CanReadAndWrite();
}
