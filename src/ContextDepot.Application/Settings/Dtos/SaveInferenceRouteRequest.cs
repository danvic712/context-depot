namespace ContextDepot.Application.Settings.Dtos;

public sealed record SaveInferenceRouteRequest(Guid? ProviderId, string? Model, int? Dimensions,
    int TimeoutSeconds, DateTimeOffset UpdatedAt, DateTimeOffset? ProviderUpdatedAt);
