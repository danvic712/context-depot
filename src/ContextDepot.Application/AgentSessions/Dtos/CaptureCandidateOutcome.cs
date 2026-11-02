namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record CaptureCandidateOutcome(int Index, string Outcome, Guid? ContextId = null);
