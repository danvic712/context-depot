using ContextDepot.Domain.Access;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Setup.Contracts;

public sealed record SetupResources(Workspace Workspace)
{
    public bool ExistingWorkspace { get; init; }
    public IReadOnlyList<InferenceProvider> Providers { get; init; } = [];
    public IReadOnlyList<InferenceRoute> Routes { get; init; } = [];
    public AccessKey? AccessKey { get; init; }
}
