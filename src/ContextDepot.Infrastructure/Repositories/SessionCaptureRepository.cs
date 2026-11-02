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
    IOptionsMonitor<SessionCaptureOptions> options, TimeProvider clock) : ISessionCaptureRepository
{
    public async Task<SessionCaptureReceipt> EnqueueAsync(SessionCaptureEvent message, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockKeyAsync(message, cancellationToken);
            var receipt = await EnqueueMessageAsync(message, cancellationToken);
            if (!options.CurrentValue.Enabled)
            {
                db.ChangeTracker.Clear();
                return new("disabled");
            }
            await transaction.CommitAsync(cancellationToken);
            return receipt;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<SessionCaptureReceipt> EnqueueTurnAsync(SessionCaptureEvent user, SessionCaptureEvent assistant,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockKeyAsync(user, cancellationToken);
            // Sensitive turns are tombstones from their first write, never encrypt-then-purge.
            if ((user.EncryptedContent is null && user.ResultCode == "skipped_sensitive") ||
                (assistant.EncryptedContent is null && assistant.ResultCode == "skipped_sensitive"))
            {
                foreach (var message in new[] { user, assistant })
                {
                    message.EncryptedContent = null;
                    message.ResultCode = "skipped_sensitive";
                }
            }
            var receipt = await EnqueueMessageAsync(user, cancellationToken);
            if (receipt.Status == "disabled") return receipt;
            var assistantReceipt = await EnqueueMessageAsync(assistant, cancellationToken);
            if (assistantReceipt.Status == "disabled" || !options.CurrentValue.Enabled)
            {
                db.ChangeTracker.Clear();
                return new("disabled");
            }
            await transaction.CommitAsync(cancellationToken);
            return receipt;
        }
        catch
        {
            // A failed second-phase write must not leave a tracked first-phase insert after rollback.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task LockKeyAsync(SessionCaptureEvent message, CancellationToken cancellationToken)
    {
        await SessionCaptureLocks.LockKeyAsync(db, message.DepotId, message.AccessKeyId, cancellationToken);
    }

    private async Task<SessionCaptureReceipt> EnqueueMessageAsync(SessionCaptureEvent message, CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled) return new("disabled");
        // Auto tombstones remain the mode authority even after promotion.
        if (await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().AnyAsync(r => r.DepotId == message.DepotId &&
                r.AccessKeyId == message.AccessKeyId && r.Agent == message.Agent && r.ClientSessionId == message.ClientSessionId &&
                r.TurnId == message.TurnId, cancellationToken) ||
            await db.SessionCaptureInboxEvents.IgnoreQueryFilters().AnyAsync(e => e.DepotId == message.DepotId &&
                e.AccessKeyId == message.AccessKeyId && e.Agent == message.Agent && e.ClientSessionId == message.ClientSessionId &&
                e.EventId == message.EventId, cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureConflict);
        var existing = await db.SessionCaptureEvents.IgnoreQueryFilters().AsNoTracking().Where(e =>
            e.DepotId == message.DepotId && e.AccessKeyId == message.AccessKeyId && e.Agent == message.Agent &&
            e.ClientSessionId == message.ClientSessionId && (e.EventId == message.EventId ||
                (e.TurnId == message.TurnId && e.Phase == message.Phase))).ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            if (existing.Any(e => e.ContentHash != message.ContentHash || e.WorkspaceId != message.WorkspaceId ||
                    e.TurnId != message.TurnId || e.Phase != message.Phase || e.SessionId != message.SessionId))
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureConflict);
            await SessionAccessGuard.EnsureAsync(db, message.DepotId, message.AccessKeyId, message.WorkspaceId, cancellationToken);
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
            SessionLifecyclePolicy.EnsureNewCaptureAllowed(session);
        }
        // The opposite phase cannot silently bind a turn to another workspace.
        if (await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.DepotId == message.DepotId &&
            e.AccessKeyId == message.AccessKeyId && e.Agent == message.Agent && e.ClientSessionId == message.ClientSessionId &&
            e.TurnId == message.TurnId && (e.WorkspaceId != message.WorkspaceId || e.SessionId != message.SessionId), cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureConflict);
        var partners = await db.SessionCaptureEvents.FromSqlInterpolated($"""
            SELECT * FROM public.session_capture_events WHERE depot_id = {message.DepotId} AND access_key_id = {message.AccessKeyId}
              AND agent = {message.Agent} AND client_session_id = {message.ClientSessionId} AND turn_id = {message.TurnId}
            ORDER BY id FOR UPDATE
            """).IgnoreQueryFilters().ToArrayAsync(cancellationToken);
        foreach (var partner in partners) await db.Entry(partner).ReloadAsync(cancellationToken);
        await SessionAccessGuard.EnsureAsync(db, message.DepotId, message.AccessKeyId, message.WorkspaceId, cancellationToken);
        if (!options.CurrentValue.Enabled) return new("disabled");
        var now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
        var terminal = partners.FirstOrDefault(e => e.Status is "completed" or "failed");
        var sensitive = message.EncryptedContent is null && message.ResultCode == "skipped_sensitive";
        if (terminal is not null)
        {
            message.Status = terminal.Status;
            message.ResultCode = terminal.ResultCode;
            message.LastErrorCode = terminal.LastErrorCode;
            message.ProcessedAt = now;
            SessionCapturePolicy.Purge(message, now);
        }
        else if (sensitive)
        {
            foreach (var item in partners.Append(message))
            {
                SessionCapturePolicy.Complete(item, "skipped_sensitive", SessionCaptureDisposition.Completed, 0, now);
                SessionCapturePolicy.Purge(item, now);
            }
        }
        else
        {
            var queueCount = await db.SessionCaptureEvents.IgnoreQueryFilters().CountAsync(e => e.DepotId == message.DepotId &&
                e.AccessKeyId == message.AccessKeyId && (e.Status == "pending" || e.Status == "processing"), cancellationToken);
            var inboxCount = await (from e in db.SessionCaptureInboxEvents.IgnoreQueryFilters()
                join r in db.SessionCaptureTurnRoutes.IgnoreQueryFilters() on e.TurnRouteId equals r.Id
                where r.DepotId == message.DepotId && r.AccessKeyId == message.AccessKeyId && e.EncryptedContent != null &&
                    (r.State == "waiting_user" || r.State == "pending" || r.State == "resolving" || r.State == "resolved")
                select e.Id).CountAsync(cancellationToken);
            if (queueCount + inboxCount >= options.CurrentValue.MaxPendingEventsPerKey)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureCapacity);
        }
        db.SessionCaptureEvents.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return new("accepted", message.Id);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var candidates = await db.SessionCaptureEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(SessionCapturePolicy.RequiresCleanup(options.CurrentValue, clock.GetUtcNow()))
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).Take(256)
            .Select(e => new { e.Id, e.DepotId, e.AccessKeyId }).ToArrayAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await SessionCaptureLocks.TryLockKeyAsync(db, candidate.DepotId, candidate.AccessKeyId, cancellationToken)) continue;
            var locator = await db.SessionCaptureEvents.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == candidate.Id, cancellationToken);
            var messages = await db.SessionCaptureEvents.FromSqlInterpolated($"""
                SELECT * FROM public.session_capture_events WHERE depot_id = {locator.DepotId} AND access_key_id = {locator.AccessKeyId}
                AND agent = {locator.Agent} AND client_session_id = {locator.ClientSessionId} AND turn_id = {locator.TurnId}
                ORDER BY id FOR UPDATE
                """).IgnoreQueryFilters().ToArrayAsync(cancellationToken);
            foreach (var message in messages) await db.Entry(message).ReloadAsync(cancellationToken);
            var now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
            var user = messages.FirstOrDefault(e => e.Phase == "user");
            var exhausted = user is not null && user.Status == "processing" && user.LeaseUntil <= now &&
                (user.EncryptedExtractionPlan == null ? user.AttemptCount >= options.CurrentValue.MaxAttempts :
                 user.PersistenceAttemptCount >= options.CurrentValue.PersistenceMaxAttempts);
            foreach (var message in messages)
            {
                SessionCapturePolicy.Cleanup(message, messages.Length == 2, options.CurrentValue, now);
                if (exhausted && message.Status is "pending" or "processing")
                    SessionCapturePolicy.Complete(message, "retry_exhausted", SessionCaptureDisposition.Failed, 0, now);
            }
            if (user is not null && HasSavedOutcome(user))
                foreach (var message in messages.Where(e => e.Status is "completed" or "failed")) message.ResultCode = "saved";
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<CapturedTurn?> ClaimAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled) return null;
        var now = clock.GetUtcNow();
        var pending = db.SessionCaptureEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(SessionCapturePolicy.WithinRetention(options.CurrentValue, now))
            .Where(e => (e.Status == "pending" && e.NextAttemptAt <= now) || (e.Status == "processing" && e.LeaseUntil <= now));
        var candidates = await (from user in pending
            from assistant in pending
            where user.Phase == "user" && assistant.Phase == "assistant" && user.SessionId == assistant.SessionId &&
                user.DepotId == assistant.DepotId && user.AccessKeyId == assistant.AccessKeyId && user.WorkspaceId == assistant.WorkspaceId &&
                user.Agent == assistant.Agent && user.ClientSessionId == assistant.ClientSessionId && user.TurnId == assistant.TurnId &&
                ((user.EncryptedExtractionPlan == null && user.AttemptCount < options.CurrentValue.MaxAttempts) ||
                 (user.EncryptedExtractionPlan != null && user.PersistenceAttemptCount < options.CurrentValue.PersistenceMaxAttempts))
            orderby user.NextAttemptAt, user.CreatedAt, user.Id
            select new { UserId = user.Id, AssistantId = assistant.Id, user.DepotId, user.AccessKeyId }).Take(64).ToArrayAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await SessionCaptureLocks.TryLockKeyAsync(db, candidate.DepotId, candidate.AccessKeyId, cancellationToken)) continue;
            var messages = await SessionCaptureLocks.LockTurnAsync(db, candidate.UserId, candidate.AssistantId, cancellationToken);
            // A previous iteration may have tracked these rows: refresh only after taking their locks.
            foreach (var message in messages) await db.Entry(message).ReloadAsync(cancellationToken);
            now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
            if (!options.CurrentValue.Enabled || messages.Length != 2 || messages.Any(e =>
                !((e.Status == "pending" && e.NextAttemptAt <= now) || (e.Status == "processing" && e.LeaseUntil <= now)))) continue;
            var user = messages.Single(e => e.Phase == "user");
            var assistant = messages.Single(e => e.Phase == "assistant");
            if (user.SessionId != assistant.SessionId || user.WorkspaceId != assistant.WorkspaceId ||
                user.DepotId != assistant.DepotId || user.AccessKeyId != assistant.AccessKeyId || user.Agent != assistant.Agent ||
                user.ClientSessionId != assistant.ClientSessionId || user.TurnId != assistant.TurnId ||
                (user.EncryptedExtractionPlan == null ? user.AttemptCount >= options.CurrentValue.MaxAttempts :
                 user.PersistenceAttemptCount >= options.CurrentValue.PersistenceMaxAttempts)) continue;
            try { await SessionAccessGuard.EnsureAsync(db, user.DepotId, user.AccessKeyId, user.WorkspaceId, cancellationToken); }
            catch (ContextDepotApplicationException)
            {
                var saved = HasSavedOutcome(user);
                foreach (var message in messages)
                {
                    SessionCapturePolicy.Complete(message, saved ? "saved" : "skipped_unauthorized", SessionCaptureDisposition.Completed, 0, now);
                    SessionCapturePolicy.Purge(message, now);
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }
            // Preflight only: actual requests still require an atomic provider reservation.
            // Skip budget-blocked keys so one saturated key cannot stall this cycle.
            var waitUntil = user.EncryptedExtractionPlan is null ? await SessionCaptureCallBudget.FindWaitUntilAsync(
                db, user.DepotId, user.AccessKeyId, options.CurrentValue.MaxCallsPerHour, now, cancellationToken) : null;
            if (waitUntil is not null)
            {
                foreach (var message in messages)
                {
                    if ((message.CaptureExpiresAt ?? message.CreatedAt.AddHours(options.CurrentValue.RetentionHours)) <= waitUntil)
                    {
                        SessionCapturePolicy.Complete(message, HasSavedOutcome(user) ? "saved" : "skipped_expired", SessionCaptureDisposition.Completed, 0, now);
                        SessionCapturePolicy.Purge(message, now);
                    }
                    else
                    {
                        message.Status = "pending";
                        message.ResultCode = null;
                        message.NextAttemptAt = waitUntil.Value;
                        message.LeaseToken = null;
                        message.LeaseUntil = null;
                    }
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }
            var lease = SessionCapturePolicy.TryClaim(messages, 0, options.CurrentValue, now);
            if (lease is null) continue;
            if (user.EncryptedExtractionPlan is not null) user.PersistenceAttemptCount++;
            var expiry = user.CaptureExpiresAt ?? user.CreatedAt.AddHours(options.CurrentValue.RetentionHours);
            var turn = new CapturedTurn(user.Id, assistant.Id, user.DepotId, user.AccessKeyId, user.WorkspaceId,
                lease.Value, string.Empty, string.Empty, user.Agent, user.LeaseToken, expiry);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            try
            {
                var plan = user.EncryptedExtractionPlan is null ? null : CapturePlanCodec.Read(
                    protector.Unprotect(user.EncryptedExtractionPlan), user.ExtractionPlanHash ?? string.Empty);
                if (plan is not null && user.ExtractorVersion != plan.Version) throw new CryptographicException();
                return turn with { UserContent = DecryptChecked(user), AssistantContent = DecryptChecked(assistant), Plan = plan };
            }
            catch (Exception exception) when (exception is CryptographicException or System.Text.Json.JsonException or InvalidOperationException)
            {
                await FinishAsync(turn, new(SessionCaptureDisposition.Failed, "content_unavailable"), cancellationToken);
                return null;
            }
        }
        return null;
    }

    private string DecryptChecked(SessionCaptureEvent message)
    {
        if (message.EncryptedContent is null) throw new CryptographicException();
        var content = protector.Unprotect(message.EncryptedContent);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        if (!string.Equals(hash, message.ContentHash, StringComparison.Ordinal)) throw new CryptographicException();
        return content;
    }

    private static bool HasSavedOutcome(SessionCaptureEvent user)
    {
        using var outcomes = System.Text.Json.JsonDocument.Parse(user.CandidateOutcomesJson);
        return outcomes.RootElement.EnumerateArray().Any(e => e.TryGetProperty("outcome", out var outcome) && outcome.GetString() == "created");
    }

    public Task FinishAsync(CapturedTurn turn, string resultCode, SessionCaptureDisposition disposition, CancellationToken cancellationToken) =>
        FinishAsync(turn, new(disposition, resultCode), cancellationToken);

    public async Task FinishAsync(CapturedTurn turn, CaptureProcessingResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SessionCaptureLocks.LockKeyAsync(db, turn.DepotId, turn.AccessKeyId, cancellationToken);
        var messages = await SessionCaptureLocks.LockTurnAsync(db, turn.UserEventId, turn.AssistantEventId, cancellationToken);
        foreach (var message in messages) await db.Entry(message).ReloadAsync(cancellationToken);
        var now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
        if (turn.LeaseToken is null || messages.Length != 2 || messages.Any(e => e.DepotId != turn.DepotId ||
            e.AccessKeyId != turn.AccessKeyId || e.WorkspaceId != turn.WorkspaceId || e.Status != "processing" ||
            e.LeaseToken != turn.LeaseToken || e.LeaseUntil <= now || e.LeaseUntil == null ||
            (e.CaptureExpiresAt ?? e.CreatedAt.AddHours(options.CurrentValue.RetentionHours)) <= now)) return;
        var user = messages.Single(e => e.Phase == "user");
        var saved = HasSavedOutcome(user);
        var disposition = result.Disposition;
        var code = saved ? "saved" : result.ResultCode;
        var error = result.LastErrorCode;
        var purge = result.ResultCode is "skipped_sensitive" or "skipped_unauthorized" or "skipped_expired";
        try { await SessionAccessGuard.EnsureAsync(db, turn.DepotId, turn.AccessKeyId, turn.WorkspaceId, cancellationToken); }
        catch (ContextDepotApplicationException) { disposition = SessionCaptureDisposition.Completed; code = saved ? "saved" : "skipped_unauthorized"; purge = true; }
        if (!options.CurrentValue.Enabled && !purge)
        {
            disposition = SessionCaptureDisposition.Retry;
            code = null;
        }
        var attempts = user.EncryptedExtractionPlan == null ? user.AttemptCount : user.PersistenceAttemptCount;
        var maxAttempts = user.EncryptedExtractionPlan == null ? options.CurrentValue.MaxAttempts : options.CurrentValue.PersistenceMaxAttempts;
        if (disposition == SessionCaptureDisposition.Retry && attempts >= maxAttempts)
        { disposition = SessionCaptureDisposition.Failed; code = saved ? "saved" : "retry_exhausted"; }
        var nextBudgetAttempt = result.NotBefore ?? user.NextAttemptAt;
        foreach (var message in messages)
        {
            // Persistence retries are not bounded by the exhausted extraction counter.
            SessionCapturePolicy.Complete(message, code, disposition, int.MaxValue, now);
            if (error == "budget_wait" && message.Status == "pending" && nextBudgetAttempt > now)
                message.NextAttemptAt = nextBudgetAttempt;
            message.LastErrorCode = error ?? message.LastErrorCode;
            if (purge) SessionCapturePolicy.Purge(message, now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
