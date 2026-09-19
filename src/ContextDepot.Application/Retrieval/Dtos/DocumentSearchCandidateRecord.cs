using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Retrieval.Dtos;

public sealed record DocumentSearchCandidateRecord(
    BootstrapDocumentChunkCandidate Document,
    string WorkspacePath)
{
    public Guid DocumentChunkId => Document.Id;

    public Guid DocumentId => Document.DocumentId;

    public Guid WorkspaceId => Document.WorkspaceId;
}
