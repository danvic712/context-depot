namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionStatusResult(SessionModel Session, SessionEventCounts Events, SessionReceiptStatus? Receipt);
