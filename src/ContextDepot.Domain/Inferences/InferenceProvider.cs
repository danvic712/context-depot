namespace ContextDepot.Domain.Inferences;

public sealed class InferenceProvider
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ProtocolCode { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string? ProtectedApiKey { get; set; }

    public string VerificationState { get; set; } = "unverified";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<InferenceRoute> Routes { get; } = new List<InferenceRoute>();
}
