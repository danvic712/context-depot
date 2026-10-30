namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionReceiptStatus(Guid ReceiptId, string Phase, string Status, int AttemptCount,
    string? ResultCode, DateTimeOffset CreatedAt, DateTimeOffset? ProcessedAt, bool ContentPurged);
