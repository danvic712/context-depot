using ContextDepot.Application.Documents.Dtos;

namespace ContextDepot.Application.Documents.Contracts;

public interface IDocumentAppService
{
    Task<DocumentModel> UpsertAsync(UpsertDocumentCommand command, CancellationToken cancellationToken);

    Task<DocumentContentModel?> GetAsync(Guid documentId, CancellationToken cancellationToken);

    Task<Guid> ResolveLinkAsync(Guid sourceId, string path, CancellationToken cancellationToken, Guid? workspaceId = null);

    Task ArchiveAsync(Guid documentId, CancellationToken cancellationToken);
}
