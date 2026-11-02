using ContextDepot.Domain.AgentSessions;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.AgentSessions;

/// <summary>Capture lock order: Key advisory lock, UUID-ordered rows, access guard, workspace write lock.</summary>
internal static class SessionCaptureLocks
{
    internal static Task<DateTimeOffset> NowAsync(ContextDepotDbContext db, CancellationToken ct) =>
        db.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    internal static Task LockKeyAsync(ContextDepotDbContext db, Guid depotId, Guid keyId, CancellationToken ct)
    {
        var key = $"session-capture:{depotId}:{keyId}";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    internal static async Task<bool> TryLockKeyAsync(ContextDepotDbContext db, Guid depotId, Guid keyId, CancellationToken ct)
    {
        var key = $"session-capture:{depotId}:{keyId}";
        return await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"").SingleAsync(ct);
    }

    internal static Task<SessionCaptureEvent[]> LockTurnAsync(ContextDepotDbContext db, Guid userId, Guid assistantId, CancellationToken ct) =>
        db.SessionCaptureEvents.FromSqlInterpolated($"SELECT * FROM public.session_capture_events WHERE id IN ({userId}, {assistantId}) ORDER BY id FOR UPDATE")
            .IgnoreQueryFilters().ToArrayAsync(ct);
}
