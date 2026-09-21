using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.SemanticRetrieval.Dtos;

public sealed record SemanticContextCandidateRecord(
    BootstrapContextCandidate Context,
    double Similarity)
{
    public Guid ContextItemId => Context.Id;

    public Guid DepotId => Context.DepotId;

    public Guid WorkspaceId => Context.WorkspaceId;
}
