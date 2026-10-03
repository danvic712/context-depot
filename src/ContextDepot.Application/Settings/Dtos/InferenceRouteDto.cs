namespace ContextDepot.Application.Settings.Dtos;

public sealed record InferenceRouteDto(string Capability, string? ProviderName, string Protocol, string? Endpoint,
    string? Model, int? Dimensions, int TimeoutSeconds, bool HasApiKey, DateTimeOffset UpdatedAt,
    string RuntimeState, string IndexState, bool IsApplied, Guid? ProviderId = null);
