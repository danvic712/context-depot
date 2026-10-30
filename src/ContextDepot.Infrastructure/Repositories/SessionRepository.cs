using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SessionRepository(ContextDepotDbContext db) : ISessionRepository
{
    public async Task<AgentSession> StartAsync(AgentSession session, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(session.DepotId, session.AccessKeyId, cancellationToken);
        await SessionAccessGuard.EnsureAsync(db, session.DepotId, session.AccessKeyId, session.WorkspaceId, cancellationToken);
        var existing = await db.AgentSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s =>
            s.DepotId == session.DepotId && s.AccessKeyId == session.AccessKeyId && s.Agent == session.Agent &&
            s.ClientSessionId == session.ClientSessionId, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != session.WorkspaceId) throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionConflict);
            return existing;
        }
        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public Task<AgentSession?> GetAsync(Guid depotId, Guid accessKeyId, Guid sessionId, CancellationToken cancellationToken) =>
        Sessions(depotId, accessKeyId).AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task<AgentSession> EndAsync(Guid depotId, Guid accessKeyId, Guid sessionId, DateTimeOffset now,
        string reason, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(depotId, accessKeyId, cancellationToken);
        var session = await Sessions(depotId, accessKeyId).SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
        await SessionAccessGuard.EnsureAsync(db, depotId, accessKeyId, session.WorkspaceId, cancellationToken);
        SessionLifecyclePolicy.End(session, now, reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task<SessionEventCounts> CountEventsAsync(Guid depotId, Guid accessKeyId, Guid sessionId, CancellationToken cancellationToken)
    {
        var events = Events(depotId, accessKeyId, sessionId);
        var groups = await events.GroupBy(e => e.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(cancellationToken);
        int Count(string status) => groups.FirstOrDefault(g => g.Status == status)?.Count ?? 0;
        var unpaired = await events.CountAsync(e => !db.SessionCaptureEvents.IgnoreQueryFilters().Any(partner =>
            partner.SessionId == e.SessionId && partner.TurnId == e.TurnId && partner.Phase != e.Phase), cancellationToken);
        var saved = await events.CountAsync(e => e.Phase == "user" && e.ResultCode == "saved", cancellationToken);
        return new(Count("pending"), Count("processing"), Count("completed"), Count("failed"), unpaired, saved);
    }

    public Task<SessionReceiptStatus?> GetReceiptAsync(Guid depotId, Guid accessKeyId, Guid sessionId, Guid receiptId,
        CancellationToken cancellationToken) => Events(depotId, accessKeyId, sessionId).Where(e => e.Id == receiptId)
        .Select(e => new SessionReceiptStatus(e.Id, e.Phase, e.Status, e.AttemptCount, e.ResultCode, e.CreatedAt,
            e.ProcessedAt, e.EncryptedContent == null)).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<AgentSession> Sessions(Guid depotId, Guid keyId) => db.AgentSessions.IgnoreQueryFilters().Where(s =>
        s.DepotId == depotId && s.AccessKeyId == keyId &&
        db.DepotAccessKeys.IgnoreQueryFilters().Any(k => k.Id == keyId && k.DepotId == depotId && k.RevokedAt == null) &&
        db.WorkspaceAccessGrants.IgnoreQueryFilters().Any(g => g.DepotId == depotId && g.DepotAccessKeyId == keyId && g.WorkspaceId == s.WorkspaceId));

    private IQueryable<SessionCaptureEvent> Events(Guid depotId, Guid keyId, Guid sessionId) => db.SessionCaptureEvents.IgnoreQueryFilters()
        .Where(e => e.DepotId == depotId && e.AccessKeyId == keyId && e.SessionId == sessionId && Sessions(depotId, keyId).Any(s => s.Id == sessionId));

    private Task<int> LockKeyAsync(Guid depotId, Guid keyId, CancellationToken cancellationToken)
    {
        var key = $"session-capture:{depotId}:{keyId}";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
