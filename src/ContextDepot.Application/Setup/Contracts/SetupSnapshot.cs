using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Application.Setup.Contracts;

public sealed record SetupSnapshot(InstallationState Installation, Workspace? Workspace, bool HasWorkspaces)
{
    public IReadOnlyList<InferenceProvider> Providers { get; init; } = [];
    public IReadOnlyList<InferenceRoute> Routes { get; init; } = [];
}
