using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Retrieval.Dtos;

public sealed record ContextSearchCandidateRecord(
    BootstrapContextCandidate Context,
    string WorkspacePath)
{
    public Guid ContextItemId => Context.Id;

    public Guid WorkspaceId => Context.WorkspaceId;
}
