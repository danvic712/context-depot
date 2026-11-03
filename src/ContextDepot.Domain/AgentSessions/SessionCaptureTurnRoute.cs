namespace ContextDepot.Domain.AgentSessions;

public sealed class SessionCaptureTurnRoute
{
    public Guid Id { get; set; }
    public Guid DepotId { get; set; }
    public Guid AccessKeyId { get; set; }
    public string Agent { get; set; } = string.Empty;
    public string ClientSessionId { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public Guid? WorkspaceId { get; set; }
    public string State { get; set; } = string.Empty;
    public string? ResultCode { get; set; }
    public string? LastErrorCode { get; set; }
    public string? ResolverVersion { get; set; }
    public int AttemptCount { get; set; }
    public int ProviderCallCount { get; set; }
    public DateTimeOffset? LastProviderCallAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? PromotedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? EncryptedRoutingVector { get; set; }
    public string? RoutingVectorHash { get; set; }
    public string? RoutingEmbeddingFingerprint { get; set; }
}
