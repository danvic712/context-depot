namespace ContextDepot.Application.Settings.Dtos;

public sealed record InferenceProviderDto(Guid Id, string Name, string Protocol, string? Endpoint, bool HasApiKey,
    DateTimeOffset UpdatedAt, string Kind = "custom");
