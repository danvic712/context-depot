namespace ContextDepot.Application.Settings.Dtos;

public sealed record SaveInferenceProviderRequest(Guid? Id, string Name, string Endpoint, string? ApiKey,
    DateTimeOffset? UpdatedAt, InferenceProviderModelRequest? Embedding, InferenceProviderModelRequest? Chat,
    DateTimeOffset EmbeddingUpdatedAt, DateTimeOffset ChatUpdatedAt, string Kind = "custom")
{
    public override string ToString() => $"{nameof(SaveInferenceProviderRequest)} {{ ApiKey = [REDACTED] }}";
}
