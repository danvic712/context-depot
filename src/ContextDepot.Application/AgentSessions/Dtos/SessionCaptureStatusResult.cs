namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionCaptureStatusResult(Guid? ReceiptId, Guid? TurnReceiptId,
    string RoutingState, string ProcessingState, string? ResultCode, string? LastErrorCode,
    string? Workspace, IReadOnlyList<SessionCapturePhaseStatus> Messages,
    IReadOnlyList<Guid> CreatedContextIds, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt,
    DateTimeOffset? NextAttemptAt, int RoutingAttempts, int ExtractionAttempts);
