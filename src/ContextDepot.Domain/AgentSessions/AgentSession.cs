namespace ContextDepot.Domain.AgentSessions;

/// <summary>A client session bound to one authenticated key and Workspace.</summary>
public sealed class AgentSession
{
    public Guid Id { get; set; }
    public Guid DepotId { get; set; }
    public Guid AccessKeyId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Agent { get; set; } = string.Empty;
    public string ClientSessionId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? EndReason { get; set; }
}
