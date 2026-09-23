namespace ContextDepot.Domain.Inferences;

public sealed class InferenceRoute
{
    public Guid Id { get; set; }

    public string Capability { get; set; } = string.Empty;

    public Guid? ProviderId { get; set; }

    public InferenceProvider? Provider { get; set; }

    public string? ModelName { get; set; }

    public int? Dimensions { get; set; }

    public int TimeoutSeconds { get; set; }

    public string? EmbeddingProfileFingerprint { get; set; }

    public string IndexState { get; set; } = "unconfigured";

    public long IndexGeneration { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
