using ContextDepot.Application.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.AgentSessions;

/// <summary>Locks the live key/grant for an authorized session mutation.</summary>
internal static class SessionAccessGuard
{
    public static async Task EnsureAsync(ContextDepotDbContext db, Guid depotId, Guid keyId, Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var keys = await db.DepotAccessKeys.FromSqlInterpolated($"""
            SELECT k.* FROM public.depot_access_keys k
            JOIN public.workspace_access_grants g ON g.depot_access_key_id = k.id AND g.depot_id = k.depot_id
            WHERE k.id = {keyId} AND k.depot_id = {depotId} AND k.revoked_at IS NULL
              AND g.workspace_id = {workspaceId} FOR SHARE OF k, g
            """).AsNoTracking().ToArrayAsync(cancellationToken);
        if (keys.Length != 1) throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
    }
}
