namespace ContextDepot.Domain.AgentSessions;

/// <summary>An encrypted, idempotent visible message awaiting paired-turn processing.</summary>
public sealed class SessionCaptureEvent
{
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
