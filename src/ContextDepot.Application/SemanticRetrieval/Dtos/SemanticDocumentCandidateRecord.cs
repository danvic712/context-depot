using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.SemanticRetrieval.Dtos;

public sealed record SemanticDocumentCandidateRecord(
    BootstrapDocumentChunkCandidate Document,
    double Similarity)
{
    public Guid DocumentChunkId => Document.Id;

    public Guid DocumentId => Document.DocumentId;

    public Guid DepotId => Document.DepotId;

    public Guid WorkspaceId => Document.WorkspaceId;
}
