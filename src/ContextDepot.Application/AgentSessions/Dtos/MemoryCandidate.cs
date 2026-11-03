namespace ContextDepot.Application.AgentSessions.Dtos;

// Legacy quote fields remain available for callers; provider responses use Summary/Quotes only.
public sealed record MemoryCandidate(string Kind, string Key, string Content = "", string Evidence = "",
    decimal Confidence = 0.95m, string? Summary = null, IReadOnlyList<string>? Quotes = null);
