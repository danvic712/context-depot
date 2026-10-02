namespace ContextDepot.Application.Settings.Dtos;

public sealed record AiRouteDto(string Capability, string? ProviderName, string Protocol, string? Endpoint,
    string? Model, int? Dimensions, int TimeoutSeconds, bool HasApiKey, DateTimeOffset UpdatedAt,
    string RuntimeState, string IndexState, bool IsApplied);

public sealed record SaveAiRouteRequest(string ProviderName, string Endpoint, string Model, int? Dimensions,
    int TimeoutSeconds, string? ApiKey, DateTimeOffset UpdatedAt)
{
    public override string ToString() => $"{nameof(SaveAiRouteRequest)} {{ ApiKey = [REDACTED] }}";
}
