using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Infrastructure.Access;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed class SessionCaptureAuthorization(ContextDepotDbContext db, WorkspaceAccessContext current,
    IWorkspaceAppService workspaces) : ISessionCaptureAuthorization
{
    public async Task<string?> AuthorizeAsync(CapturedTurn turn, CancellationToken cancellationToken)
    {
        var now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
        if (turn.EffectiveExpiresAt <= now) return null;
        if (!await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.Id == turn.UserEventId && e.AccessKeyId == turn.AccessKeyId && e.WorkspaceId == turn.WorkspaceId &&
                e.Status == "processing" && turn.LeaseToken != null && e.LeaseToken == turn.LeaseToken &&
                e.LeaseUntil > now && (e.CaptureExpiresAt == null || e.CaptureExpiresAt > now),
                cancellationToken)) return null;
        var key = await db.AccessKeys.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(k => k.Id == turn.AccessKeyId && k.RevokedAt == null, cancellationToken);
        if (key is null || !await db.WorkspaceAccessGrants.IgnoreQueryFilters().AnyAsync(g =>
                g.AccessKeyId == turn.AccessKeyId && g.WorkspaceId == turn.WorkspaceId, cancellationToken))
            return null;
        if (current.AccessKeyId is null)
            current.Initialize(new AccessKeyIdentity(key.Id,
                [turn.WorkspaceId], [turn.WorkspaceId], false, key.AccessVersion));
        else if (current.AccessKeyId != key.Id) return null;
        return (await workspaces.GetAsync(turn.WorkspaceId, cancellationToken))?.Path;
    }
}
