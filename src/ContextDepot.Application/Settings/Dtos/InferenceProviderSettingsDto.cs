namespace ContextDepot.Application.Settings.Dtos;

public sealed record InferenceProviderSettingsDto(IReadOnlyList<InferenceProviderDto> Providers, IReadOnlyList<InferenceRouteDto> Routes)
{
    public IReadOnlyList<InferenceProviderPresetDto> Presets { get; init; } = InferenceProviderCatalog.Presets;
}
