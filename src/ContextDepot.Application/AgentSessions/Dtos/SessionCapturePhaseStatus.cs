namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionCapturePhaseStatus(string Phase, Guid ReceiptId, bool ContentPurged);
