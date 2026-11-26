namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record CapturedTurn(Guid UserEventId, Guid AssistantEventId, Guid AccessKeyId,
    Guid WorkspaceId, DateTimeOffset LeaseUntil, string UserContent, string AssistantContent, string? Agent = null,
    Guid? LeaseToken = null, DateTimeOffset? EffectiveExpiresAt = null, ValidatedExtractionPlan? Plan = null);
