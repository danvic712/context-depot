namespace ContextDepot.Infrastructure.Configuration;

public sealed class InferenceProviderRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ProtocolCode { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string? ProtectedApiKey { get; set; }

    public string VerificationState { get; set; } = "unverified";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<InferenceRouteRecord> Routes { get; } = new List<InferenceRouteRecord>();
}
