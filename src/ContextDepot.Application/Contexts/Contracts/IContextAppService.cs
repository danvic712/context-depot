using ContextDepot.Application.Contexts.Dtos;

namespace ContextDepot.Application.Contexts.Contracts;

public interface IContextAppService
{
    Task<SaveContextResult> SaveAsync(SaveContextCommand command, CancellationToken cancellationToken);

    ContextDepot.Application.AgentSessions.Dtos.PreparedCaptureContext PrepareCaptureContext(
        ContextDepot.Application.AgentSessions.Dtos.CapturedTurn turn,
        ContextDepot.Application.AgentSessions.Dtos.ValidatedMemoryCandidate candidate, string workspacePath) =>
        throw new NotSupportedException();

    Task ArchiveAsync(Guid contextId, CancellationToken cancellationToken);
}
