using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Setup.Dtos;

public sealed record CompleteSetupRequest(SetupWorkspaceRequest Workspace,
    IReadOnlyList<SaveInferenceProviderRequest> Providers, string? AccessKeyName = null)
{
    public override string ToString() => $"{nameof(CompleteSetupRequest)} {{ Providers = [REDACTED] }}";
}
