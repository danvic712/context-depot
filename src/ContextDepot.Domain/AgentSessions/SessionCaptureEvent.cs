namespace ContextDepot.Domain.AgentSessions;

/// <summary>An encrypted, idempotent visible message awaiting paired-turn processing.</summary>
public sealed class SessionCaptureEvent
{
    public DateTimeOffset? CaptureExpiresAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? ContentPurgedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public int ProviderCallCount { get; set; }
    public DateTimeOffset? LastProviderCallAt { get; set; }
    public string? EncryptedExtractionPlan { get; set; }
    public string? ExtractionPlanHash { get; set; }
    public string? ExtractorVersion { get; set; }
    public string CandidateOutcomesJson { get; set; } = "[]";
    public int PersistenceAttemptCount { get; set; }
    public Guid Id { get; set; }
    public Guid? SessionId { get; set; }
    public Guid DepotId { get; set; }
    public Guid AccessKeyId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Agent { get; set; } = string.Empty;
    public string ClientSessionId { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string? EncryptedContent { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? ResultCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
