namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record ValidatedMemoryCandidate(int Index, string Kind, string Key, string Summary, IReadOnlyList<EvidenceFragment> Evidence);
