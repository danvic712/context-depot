using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Setup.Contracts;

public sealed record SetupResources(Depot Depot, Workspace Workspace)
{
    public bool ExistingWorkspace { get; init; }
    public IReadOnlyList<InferenceProvider> Providers { get; init; } = [];
    public IReadOnlyList<InferenceRoute> Routes { get; init; } = [];
    public DepotAccessKey? AccessKey { get; init; }
}
