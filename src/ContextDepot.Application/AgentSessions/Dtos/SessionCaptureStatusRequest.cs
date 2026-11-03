namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionCaptureStatusRequest(Guid? ReceiptId = null, Guid? TurnReceiptId = null);
