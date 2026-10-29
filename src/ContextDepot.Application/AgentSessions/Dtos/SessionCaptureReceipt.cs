namespace ContextDepot.Application.AgentSessions.Dtos;

/// <summary>Receipt only; accepted does not imply extraction or a Context write.</summary>
public sealed record SessionCaptureReceipt(string Status, Guid? ReceiptId = null);
