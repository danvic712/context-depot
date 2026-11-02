using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.AgentSessions;

public sealed class SessionCaptureStatusAppService(ICurrentDepotContext depot, IWorkspaceAccessContext access,
    ISessionCaptureStatusRepository repository) : ISessionCaptureStatusAppService
{
    public async Task<SessionCaptureStatusResult> GetAsync(SessionCaptureStatusRequest request, CancellationToken cancellationToken)
    {
        if ((request.ReceiptId is null) == (request.TurnReceiptId is null))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSessionCapture);
        if (access.DepotAccessKeyId is not Guid keyId || depot.DepotId == Guid.Empty)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureUnavailable);
        return await repository.GetAsync(depot.DepotId, keyId, request, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureUnavailable);
    }
}
