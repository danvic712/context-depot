using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

/// <summary>Restores the submitting key's current exact workspace grant in a fresh worker scope.</summary>
public interface ISessionCaptureAuthorization
{
    Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken);
}
