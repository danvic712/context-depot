namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record MemoryCandidate(string Kind, string Key, string Content, string Evidence,
    decimal Confidence);
