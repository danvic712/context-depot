using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.AgentSessions;
using Microsoft.EntityFrameworkCore;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SessionCaptureRepository(ContextDepotDbContext db, ISessionContentProtector protector,
    IOptions<SessionCaptureOptions> options, TimeProvider clock) : ISessionCaptureRepository
{
    public async Task<SessionCaptureReceipt> EnqueueAsync(SessionCaptureEvent message, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(message, cancellationToken);
        var receipt = await EnqueueMessageAsync(message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt;
    }

    public async Task<SessionCaptureReceipt> EnqueueTurnAsync(SessionCaptureEvent user, SessionCaptureEvent assistant,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(user, cancellationToken);
        var receipt = await EnqueueMessageAsync(user, cancellationToken);
        await EnqueueMessageAsync(assistant, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt;
    }

    private async Task LockKeyAsync(SessionCaptureEvent message, CancellationToken cancellationToken)
    {
        // Serialize all captures for a key, including event IDs reused with a different workspace/turn.
        var lockKey = $"session-capture:{message.DepotId}:{message.AccessKeyId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
    }

    private async Task<SessionCaptureReceipt> EnqueueMessageAsync(SessionCaptureEvent message, CancellationToken cancellationToken)
    {
        var existing = await db.SessionCaptureEvents.IgnoreQueryFilters().AsNoTracking().Where(e =>
            e.DepotId == message.DepotId && e.AccessKeyId == message.AccessKeyId && e.Agent == message.Agent &&
            e.ClientSessionId == message.ClientSessionId && (e.EventId == message.EventId ||
                (e.TurnId == message.TurnId && e.Phase == message.Phase))).ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            if (existing.Any(e => e.ContentHash != message.ContentHash || e.WorkspaceId != message.WorkspaceId ||
                    e.TurnId != message.TurnId || e.Phase != message.Phase || e.SessionId != message.SessionId))
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureConflict);
            return new("accepted", existing[0].Id);
        }
        if (message.SessionId is null && await db.AgentSessions.IgnoreQueryFilters().AnyAsync(s =>
                s.DepotId == message.DepotId && s.AccessKeyId == message.AccessKeyId && s.Agent == message.Agent &&
                s.ClientSessionId == message.ClientSessionId, cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
        if (message.SessionId is Guid sessionId)
        {
            var session = await db.AgentSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s =>
                s.Id == sessionId && s.DepotId == message.DepotId && s.AccessKeyId == message.AccessKeyId &&
                s.WorkspaceId == message.WorkspaceId && s.Agent == message.Agent && s.ClientSessionId == message.ClientSessionId, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
            await SessionAccessGuard.EnsureAsync(db, message.DepotId, message.AccessKeyId, message.WorkspaceId, cancellationToken);
            SessionLifecyclePolicy.EnsureNewCaptureAllowed(session);
        }
        // The opposite phase cannot silently bind a turn to another workspace.
        if (await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.DepotId == message.DepotId &&
            e.AccessKeyId == message.AccessKeyId && e.Agent == message.Agent && e.ClientSessionId == message.ClientSessionId &&
            e.TurnId == message.TurnId && (e.WorkspaceId != message.WorkspaceId || e.SessionId != message.SessionId), cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureConflict);
        if (await db.SessionCaptureEvents.IgnoreQueryFilters().CountAsync(e => e.AccessKeyId == message.AccessKeyId &&
                (e.Status == "pending" || e.Status == "processing"), cancellationToken) >= options.Value.MaxPendingEventsPerKey)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureCapacity);
        var hasTerminalPartner = await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.DepotId == message.DepotId &&
            e.AccessKeyId == message.AccessKeyId && e.Agent == message.Agent && e.ClientSessionId == message.ClientSessionId &&
            e.TurnId == message.TurnId && (e.Status == "completed" || e.Status == "failed"), cancellationToken);
        SessionCapturePolicy.ApplyLateArrival(message, hasTerminalPartner, clock.GetUtcNow());
        db.SessionCaptureEvents.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return new("accepted", message.Id);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(75201901)", cancellationToken);
        // Bound housekeeping work; lifecycle decisions remain in Application.
        var candidates = await db.SessionCaptureEvents.Where(SessionCapturePolicy.RequiresCleanup(options.Value, now))
            .OrderBy(message => message.CreatedAt).Take(256)
            .Select(message => new
            {
                Message = message,
                HasPartner = db.SessionCaptureEvents.Any(partner => partner.DepotId == message.DepotId &&
                    partner.AccessKeyId == message.AccessKeyId && partner.Agent == message.Agent &&
                    partner.ClientSessionId == message.ClientSessionId && partner.TurnId == message.TurnId &&
                    partner.Phase != message.Phase)
            }).ToArrayAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            SessionCapturePolicy.Cleanup(candidate.Message, candidate.HasPartner, options.Value, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CapturedTurn?> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Claims are short; a transaction advisory lock gives pair-atomic claiming across replicas.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(75201901)", cancellationToken);
        var pending = db.SessionCaptureEvents.Where(e =>
            (e.Status == "pending" && e.NextAttemptAt <= now) || (e.Status == "processing" && e.LeaseUntil < now));
        var pair = await (from user in pending
                          from assistant in pending
                          where user.Phase == "user" && assistant.Phase == "assistant" &&
                              user.SessionId == assistant.SessionId && user.DepotId == assistant.DepotId && user.AccessKeyId == assistant.AccessKeyId &&
                              user.WorkspaceId == assistant.WorkspaceId && user.Agent == assistant.Agent &&
                              user.ClientSessionId == assistant.ClientSessionId && user.TurnId == assistant.TurnId &&
                              user.AttemptCount < options.Value.MaxAttempts && assistant.AttemptCount < options.Value.MaxAttempts
                          orderby user.NextAttemptAt
                          select new { User = user, Assistant = assistant }).FirstOrDefaultAsync(cancellationToken);
        if (pair is null) { await transaction.CommitAsync(cancellationToken); return null; }
        var hourAgo = now.AddHours(-1);
        var calls = await db.SessionCaptureEvents.Where(e => e.AccessKeyId == pair.User.AccessKeyId && e.Phase == "user" &&
            ((e.ProcessedAt >= hourAgo && e.AttemptCount > 0) || e.Status == "processing")).SumAsync(e => (int?)e.AttemptCount, cancellationToken) ?? 0;
        var lease = SessionCapturePolicy.TryClaim([pair.User, pair.Assistant], calls, options.Value, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (lease is null)
        {
            return null;
        }

        try
        {
            return new(pair.User.Id, pair.Assistant.Id, pair.User.DepotId, pair.User.AccessKeyId,
                pair.User.WorkspaceId, lease.Value, DecryptChecked(pair.User), DecryptChecked(pair.Assistant), pair.User.Agent);
        }
        catch (CryptographicException)
        {
            var turn = new CapturedTurn(pair.User.Id, pair.Assistant.Id, pair.User.DepotId, pair.User.AccessKeyId,
                pair.User.WorkspaceId, lease.Value, string.Empty, string.Empty, pair.User.Agent);
            await FinishAsync(turn, "content_unavailable", SessionCaptureDisposition.Failed, cancellationToken);
            return null;
        }
    }

    private string DecryptChecked(SessionCaptureEvent message)
    {
        if (message.EncryptedContent is null) throw new CryptographicException();
        var content = protector.Unprotect(message.EncryptedContent);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        if (!string.Equals(hash, message.ContentHash, StringComparison.Ordinal)) throw new CryptographicException();
        return content;
    }

    public async Task FinishAsync(CapturedTurn turn, string resultCode, SessionCaptureDisposition disposition, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(75201901)", cancellationToken);
        var messages = await db.SessionCaptureEvents.Where(message =>
                (message.Id == turn.UserEventId || message.Id == turn.AssistantEventId) &&
                message.Status == "processing" && message.LeaseUntil == turn.LeaseUntil)
            .ToArrayAsync(cancellationToken);
        foreach (var message in messages)
        {
            SessionCapturePolicy.Complete(message, resultCode, disposition, options.Value.MaxAttempts, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
