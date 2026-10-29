using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface IMemoryExtractionService
{
    Task<IReadOnlyList<MemoryCandidate>> ExtractAsync(string user, string assistant, CancellationToken cancellationToken);
}
