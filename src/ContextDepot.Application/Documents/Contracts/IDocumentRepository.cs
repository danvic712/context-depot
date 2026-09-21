using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Domain.Documents;

namespace ContextDepot.Application.Documents.Contracts;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid depotId, Guid documentId, CancellationToken cancellationToken);

    Task<Document?> GetByPathAsync(Guid depotId, Guid workspaceId, string normalizedPath, CancellationToken cancellationToken);

    Task MarkIndexPendingAsync(Guid depotId, Guid workspaceId, string normalizedPath, DateTimeOffset now, CancellationToken cancellationToken);

    Task<DocumentReconcilePersistenceResult> ReconcileIndexAsync(DocumentIndexWrite write, CancellationToken cancellationToken);

    Task<DocumentArchivePersistenceResult> ArchiveAsync(Guid depotId, Guid documentId, DateTimeOffset now, CancellationToken cancellationToken);
}
