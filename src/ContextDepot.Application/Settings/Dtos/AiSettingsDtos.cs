namespace ContextDepot.Application.Settings.Dtos;

public sealed record AiRouteDto(string Capability, string? ProviderName, string Protocol, string? Endpoint,
    string? Model, int? Dimensions, int TimeoutSeconds, bool HasApiKey, DateTimeOffset UpdatedAt,
    string RuntimeState, string IndexState, bool IsApplied, Guid? ProviderId = null);

public sealed record SaveAiRouteRequest(string ProviderName, string Endpoint, string Model, int? Dimensions,
    int TimeoutSeconds, string? ApiKey, DateTimeOffset UpdatedAt)
{
    public override string ToString() => $"{nameof(SaveAiRouteRequest)} {{ ApiKey = [REDACTED] }}";
}

public sealed record AiProviderDto(Guid Id, string Name, string Protocol, string? Endpoint, bool HasApiKey,
    DateTimeOffset UpdatedAt);
public sealed record AiProviderSettingsDto(IReadOnlyList<AiProviderDto> Providers, IReadOnlyList<AiRouteDto> Routes);
public sealed record AiProviderModelRequest(string Model, int? Dimensions, int TimeoutSeconds);
public sealed record SaveAiProviderRequest(Guid? Id, string Name, string Endpoint, string? ApiKey,
    DateTimeOffset? UpdatedAt, AiProviderModelRequest? Embedding, AiProviderModelRequest? Chat,
    DateTimeOffset EmbeddingUpdatedAt, DateTimeOffset ChatUpdatedAt)
{
    public override string ToString() => $"{nameof(SaveAiProviderRequest)} {{ ApiKey = [REDACTED] }}";
}
