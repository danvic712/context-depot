using ContextDepot.Application.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.AgentSessions;

/// <summary>Locks the live key/grant for an authorized session mutation.</summary>
internal static class SessionAccessGuard
{
    public static async Task EnsureAutomaticCaptureAsync(ContextDepotDbContext db, Guid keyId, CancellationToken cancellationToken)
    {
        var grants = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM public.access_keys k
            JOIN public.workspace_access_grants g ON g.access_key_id = k.id
            WHERE k.id = {keyId} AND k.revoked_at IS NULL
            FOR SHARE OF k, g
            """).ToArrayAsync(cancellationToken);
        if (grants.Length > 0) return;

        var rootCreationKeys = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM public.access_keys k
            WHERE k.id = {keyId} AND k.revoked_at IS NULL AND k.can_create_root_workspaces
            FOR SHARE OF k
            """).ToArrayAsync(cancellationToken);
        if (rootCreationKeys.Length == 0) throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
    }

    public static async Task EnsureAsync(ContextDepotDbContext db, Guid keyId, Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var keys = await db.AccessKeys.FromSqlInterpolated($"""
            SELECT k.* FROM public.access_keys k
            JOIN public.workspace_access_grants g ON g.access_key_id = k.id
            WHERE k.id = {keyId} AND k.revoked_at IS NULL
              AND g.workspace_id = {workspaceId} FOR SHARE OF k, g
            """).AsNoTracking().ToArrayAsync(cancellationToken);
        if (keys.Length != 1) throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
    }
}
