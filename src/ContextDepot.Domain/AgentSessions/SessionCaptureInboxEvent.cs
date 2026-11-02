namespace ContextDepot.Domain.AgentSessions;

public sealed class SessionCaptureInboxEvent
{
    public Guid Id { get; set; }
    public Guid TurnRouteId { get; set; }
    public Guid DepotId { get; set; }
    public Guid AccessKeyId { get; set; }
    public string Agent { get; set; } = string.Empty;
    public string ClientSessionId { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string? EncryptedContent { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ContentPurgedAt { get; set; }
}
