namespace ContextDepot.Application.AgentSessions.Dtos;

/// <summary>Session metadata only; never contains conversation text.</summary>
public sealed record SessionModel(Guid Id, string Agent, string ClientSessionId, string Workspace,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? EndedAt, string? EndReason);
