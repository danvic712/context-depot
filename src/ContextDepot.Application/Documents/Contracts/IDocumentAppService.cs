using ContextDepot.Application.Documents.Dtos;

namespace ContextDepot.Application.Documents.Contracts;

public interface IDocumentAppService
{
    Task<DocumentModel> UpsertAsync(UpsertDocumentCommand command, CancellationToken cancellationToken);

    Task<DocumentContentModel?> GetAsync(Guid documentId, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid documentId, CancellationToken cancellationToken);
}
