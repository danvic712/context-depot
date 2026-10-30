using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed class SessionCaptureAuthorization(ContextDepotDbContext db, CurrentDepotAccessContext current,
    IWorkspaceAppService workspaces, TimeProvider clock) : ISessionCaptureAuthorization
{
    public async Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken)
    {
        if (!await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.Id == turn.UserEventId &&
                e.Status == "processing" && e.LeaseUntil == turn.LeaseUntil && e.LeaseUntil > clock.GetUtcNow(),
                cancellationToken)) return null;
        var key = await db.DepotAccessKeys.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(k => k.Id == turn.AccessKeyId && k.DepotId == turn.DepotId && k.RevokedAt == null, cancellationToken);
        if (key is null || !await db.WorkspaceAccessGrants.IgnoreQueryFilters().AnyAsync(g =>
                g.DepotAccessKeyId == turn.AccessKeyId && g.DepotId == turn.DepotId && g.WorkspaceId == turn.WorkspaceId, cancellationToken))
            return null;
        if (current.DepotAccessKeyId is null)
            current.Initialize(new DepotAccessKeyIdentity(key.Id, key.DepotId, string.Empty,
                [turn.WorkspaceId], [turn.WorkspaceId], false, key.AccessVersion));
        else if (current.DepotAccessKeyId != key.Id || current.DepotId != key.DepotId) return null;
        return (await workspaces.GetAsync(turn.WorkspaceId, cancellationToken))?.Path;
    }
}
