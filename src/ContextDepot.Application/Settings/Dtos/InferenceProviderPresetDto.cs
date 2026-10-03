namespace ContextDepot.Application.Settings.Dtos;

public sealed record InferenceProviderPresetDto(string Kind, string Name, string? Endpoint, string EndpointPlaceholder,
    bool SupportsEmbedding);
