using ContextDepot.Application.AgentSessions.Dtos;
namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCapturePlanRepository
{
    Task<LeaseCommitResult> StorePlanAsync(CapturedTurn turn, ValidatedExtractionPlan plan, CancellationToken cancellationToken);
    Task<CaptureCandidateOutcome> SaveCandidateAsync(CapturedTurn turn, int index, PreparedCaptureContext prepared, CancellationToken cancellationToken);
    Task<LeaseCommitResult> RejectCandidateAsync(CapturedTurn turn, int index, string reason, CancellationToken cancellationToken);
}
