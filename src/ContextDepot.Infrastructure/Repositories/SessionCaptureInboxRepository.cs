using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SessionCaptureInboxRepository(ContextDepotDbContext db, ISessionContentProtector protector,
    IOptionsMonitor<SessionCaptureOptions> options) : ISessionCaptureInboxRepository
{
    public async Task<SessionCaptureReceipt> AcceptAsync(AuthorizedCaptureEnvelope envelope, CancellationToken cancellationToken)
    {
        var first = envelope.Messages[0];
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(first.DepotId, first.AccessKeyId, cancellationToken);
        if (!options.CurrentValue.Enabled) return new("disabled");
        try { await SessionAccessGuard.EnsureAnyAsync(db, first.DepotId, first.AccessKeyId, cancellationToken); }
        catch (ContextDepotApplicationException) { throw Unavailable(); }
        var now = await NowAsync(cancellationToken);
        if (!options.CurrentValue.Enabled) return new("disabled");
        if (await db.AgentSessions.IgnoreQueryFilters().AnyAsync(s => s.DepotId == first.DepotId && s.AccessKeyId == first.AccessKeyId &&
            s.Agent == first.Agent && s.ClientSessionId == first.ClientSessionId, cancellationToken))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
        var route = await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.DepotId == first.DepotId &&
            r.AccessKeyId == first.AccessKeyId && r.Agent == first.Agent && r.ClientSessionId == first.ClientSessionId && r.TurnId == first.TurnId, cancellationToken);
        var existing = await db.SessionCaptureInboxEvents.IgnoreQueryFilters().Where(e => e.DepotId == first.DepotId &&
            e.AccessKeyId == first.AccessKeyId && e.Agent == first.Agent && e.ClientSessionId == first.ClientSessionId &&
            (e.TurnId == first.TurnId || envelope.Messages.Select(m => m.EventId).Contains(e.EventId))).ToArrayAsync(cancellationToken);
        foreach (var message in envelope.Messages)
        {
            var matches = existing.Where(e => e.EventId == message.EventId || (e.TurnId == message.TurnId && e.Phase == message.Phase)).ToArray();
            if (matches.Any(e => e.TurnId != message.TurnId || e.Phase != message.Phase || e.EventId != message.EventId || e.ContentHash != message.ContentHash)) throw Conflict();
        }
        if (route?.State != "promoted" && await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.DepotId == first.DepotId &&
            e.AccessKeyId == first.AccessKeyId && e.Agent == first.Agent && e.ClientSessionId == first.ClientSessionId &&
            (e.TurnId == first.TurnId || envelope.Messages.Select(m => m.EventId).Contains(e.EventId)), cancellationToken)) throw Conflict();
        if (route is null)
        {
            route = new SessionCaptureTurnRoute
            {
                Id = Guid.NewGuid(), DepotId = first.DepotId, AccessKeyId = first.AccessKeyId, Agent = first.Agent,
                ClientSessionId = first.ClientSessionId, TurnId = first.TurnId, CreatedAt = now,
                ExpiresAt = now.AddHours(options.CurrentValue.RetentionHours), NextAttemptAt = now, State = "waiting_user"
            };
            db.SessionCaptureTurnRoutes.Add(route);
        }
        if (route.ExpiresAt <= now && route.State is not ("promoted" or "skipped" or "failed"))
            Terminate(route, existing, now, "skipped", "skipped_expired");
        var additions = envelope.Messages.Where(m => !existing.Any(e => e.Phase == m.Phase && e.TurnId == m.TurnId)).ToArray();
        var terminal = route.State is "skipped" or "failed";
        if (route.State == "promoted" && additions.Length > 0) throw Conflict();
        if (!terminal && !envelope.Sensitive && additions.Length > 0)
        {
            var pendingInbox = await db.SessionCaptureInboxEvents.IgnoreQueryFilters().CountAsync(e => e.DepotId == first.DepotId && e.AccessKeyId == first.AccessKeyId &&
                db.SessionCaptureTurnRoutes.IgnoreQueryFilters().Any(r => r.Id == e.TurnRouteId && r.State != "skipped" && r.State != "failed" && r.State != "promoted"), cancellationToken);
            var pendingQueue = await db.SessionCaptureEvents.IgnoreQueryFilters().CountAsync(e => e.DepotId == first.DepotId && e.AccessKeyId == first.AccessKeyId &&
                (e.Status == "pending" || e.Status == "processing"), cancellationToken);
            if (pendingInbox + pendingQueue + additions.Length > options.CurrentValue.MaxPendingEventsPerKey)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionCaptureCapacity);
        }
        foreach (var message in additions)
        {
            message.TurnRouteId = route.Id;
            message.CreatedAt = now;
            if (terminal || envelope.Sensitive) { message.EncryptedContent = null; message.ContentPurgedAt = now; }
            db.SessionCaptureInboxEvents.Add(message);
        }
        var all = existing.Where(e => e.TurnRouteId == route.Id).Concat(additions).ToArray();
        if (!terminal && route.State != "promoted")
        {
            if (envelope.Sensitive) Terminate(route, all, now, "skipped", "skipped_sensitive");
            else if (route.State == "waiting_user" && all.Any(e => e.Phase == "user")) route.State = "pending";
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var receipt = all.Single(e => e.Phase == first.Phase);
        return new(route.State is "skipped" or "failed" ? "skipped" : "accepted", receipt.Id, route.Id, route.ResultCode);
    }

    public async Task<IReadOnlyList<RouteLocator>> FindRoutingCandidatesAsync(int limit, CancellationToken cancellationToken)
    {
        var now = await NowAsync(cancellationToken);
        return await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().AsNoTracking().Where(r =>
            (r.State == "pending" && r.NextAttemptAt <= now) || (r.State == "resolving" && r.LeaseUntil < now))
            .OrderBy(r => r.NextAttemptAt).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id).Take(Math.Min(limit, 64))
            .Select(r => new RouteLocator(r.DepotId, r.AccessKeyId, r.Id)).ToArrayAsync(cancellationToken);
    }

    public async Task<RouteLease?> TryClaimRouteAsync(RouteLocator locator, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await TryLockKeyAsync(locator, cancellationToken)) return null;
        var route = await LockRouteAsync(locator, cancellationToken);
        var now = await NowAsync(cancellationToken);
        if (route is null || !options.CurrentValue.Enabled ||
            !((route.State == "pending" && route.NextAttemptAt <= now) || (route.State == "resolving" && route.LeaseUntil < now))) return null;
        var messages = await MessagesAsync(route.Id, cancellationToken);
        if (route.ExpiresAt <= now) Terminate(route, messages, now, "skipped", messages.Length == 2 ? "skipped_expired" : "skipped_incomplete");
        else if (route.AttemptCount >= options.CurrentValue.RoutingMaxAttempts) Terminate(route, messages, now, "failed", "retry_exhausted");
        else if (!await AuthorizedAsync(locator.DepotId, locator.KeyId, null, cancellationToken)) Terminate(route, messages, now, "skipped", "skipped_unauthorized");
        else
        {
            var user = messages.SingleOrDefault(m => m.Phase == "user");
            string text;
            try { text = Decrypt(user); }
            catch (CryptographicException)
            {
                Terminate(route, messages, now, "failed", "content_unavailable");
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            route.State = "resolving";
            route.LeaseToken = Guid.NewGuid();
            route.LeaseUntil = now.AddSeconds(options.CurrentValue.RoutingLeaseSeconds);
            route.LastAttemptAt = now;
            route.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(locator, route.LeaseToken.Value, route.LeaseUntil.Value, route.ExpiresAt, text, user!.ContentHash,
                route.EncryptedRoutingVector, route.RoutingVectorHash, route.RoutingEmbeddingFingerprint);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public async Task<bool> StoreRoutingVectorAsync(RouteLease lease, string encryptedVector, string hash, string fingerprint, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(lease.Locator.DepotId, lease.Locator.KeyId, cancellationToken);
        var route = await LockRouteAsync(lease.Locator, cancellationToken);
        var now = await NowAsync(cancellationToken);
        if (!ValidLease(route, lease, now) || !options.CurrentValue.Enabled) return false;
        route!.EncryptedRoutingVector = encryptedVector;
        route.RoutingVectorHash = hash;
        route.RoutingEmbeddingFingerprint = fingerprint;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteRouteAsync(RouteLease lease, RouteDecision decision, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(lease.Locator.DepotId, lease.Locator.KeyId, cancellationToken);
        var route = await LockRouteAsync(lease.Locator, cancellationToken);
        var now = await NowAsync(cancellationToken);
        if (route is null || route.State != "resolving" || route.LeaseToken != lease.LeaseToken || route.LeaseUntil <= now) return false;
        var messages = await MessagesAsync(route.Id, cancellationToken);
        if (route.ExpiresAt <= now) Terminate(route, messages, now, "skipped", "skipped_expired");
        else if (!await AuthorizedAsync(route.DepotId, route.AccessKeyId, decision.WorkspaceId, cancellationToken))
            Terminate(route, messages, now, "skipped", "skipped_unauthorized");
        else if (!options.CurrentValue.Enabled)
        {
            route.State = "pending"; route.NextAttemptAt = now.AddMinutes(2); ClearLease(route);
        }
        else
        {
            route.ResolverVersion = decision.ResolverVersion;
            switch (decision.Decision)
            {
                case "resolved" when decision.WorkspaceId is Guid workspaceId:
                    await SessionAccessGuard.EnsureAsync(db, route.DepotId, route.AccessKeyId, workspaceId, cancellationToken);
                    now = await NowAsync(cancellationToken);
                    if (!ValidLease(route, lease, now) || !options.CurrentValue.Enabled) return false;
                    route.WorkspaceId = workspaceId; route.State = "resolved"; route.ResolvedAt = now; route.ResultCode = null;
                    ClearLease(route); ClearVector(route); break;
                case "no_match": Terminate(route, messages, now, "skipped", "workspace_no_match"); break;
                case "ambiguous": Terminate(route, messages, now, "skipped", "workspace_ambiguous"); break;
                case "budget_deferred":
                    route.AttemptCount = Math.Max(0, route.AttemptCount - 1);
                    route.State = "pending"; route.NextAttemptAt = decision.NotBefore ?? now.AddMinutes(2); ClearLease(route); break;
                case "transient_failure" when route.AttemptCount < options.CurrentValue.RoutingMaxAttempts:
                    route.State = "pending"; route.LastErrorCode = decision.ResultCode ?? "routing_unavailable";
                    route.NextAttemptAt = now.AddMinutes(2); ClearLease(route); break;
                default:
                    route.LastErrorCode = decision.ResultCode;
                    Terminate(route, messages, now, "failed", decision.Decision == "transient_failure" ? "retry_exhausted" : decision.ResultCode ?? "invalid_routing_response"); break;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        now = await NowAsync(cancellationToken);
        if (route.State == "resolved" && (!options.CurrentValue.Enabled || route.ExpiresAt <= now || lease.LeaseUntil <= now))
            return false;
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RouteLocator>> FindPromotableAsync(int limit, CancellationToken cancellationToken) =>
        await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().AsNoTracking().Where(r => r.State == "resolved" &&
            db.SessionCaptureInboxEvents.IgnoreQueryFilters().Count(e => e.TurnRouteId == r.Id) == 2)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Take(limit)
            .Select(r => new RouteLocator(r.DepotId, r.AccessKeyId, r.Id)).ToArrayAsync(cancellationToken);

    public async Task<bool> PromoteAsync(RouteLocator locator, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockKeyAsync(locator.DepotId, locator.KeyId, cancellationToken);
        var route = await LockRouteAsync(locator, cancellationToken);
        if (route is null) return false;
        var messages = await MessagesAsync(route.Id, cancellationToken);
        if (route.State == "promoted")
            return await db.SessionCaptureEvents.IgnoreQueryFilters().CountAsync(e => messages.Select(m => m.Id).Contains(e.Id) &&
                e.DepotId == route.DepotId && e.AccessKeyId == route.AccessKeyId && e.WorkspaceId == route.WorkspaceId, cancellationToken) == 2;
        if (route.State != "resolved" || route.WorkspaceId is not Guid workspaceId || messages.Length != 2) return false;
        var now = await NowAsync(cancellationToken);
        if (route.ExpiresAt <= now) Terminate(route, messages, now, "skipped", "skipped_expired");
        else if (!await AuthorizedAsync(route.DepotId, route.AccessKeyId, workspaceId, cancellationToken)) Terminate(route, messages, now, "skipped", "skipped_unauthorized");
        else if (!options.CurrentValue.Enabled) return false;
        else
        {
            try { foreach (var message in messages) _ = Decrypt(message); }
            catch (CryptographicException)
            {
                Terminate(route, messages, now, "failed", "content_unavailable");
                await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return false;
            }
            await SessionAccessGuard.EnsureAsync(db, route.DepotId, route.AccessKeyId, workspaceId, cancellationToken);
            now = await NowAsync(cancellationToken);
            if (!options.CurrentValue.Enabled || route.ExpiresAt <= now) return false;
            if (await db.SessionCaptureEvents.IgnoreQueryFilters().AnyAsync(e => e.DepotId == route.DepotId && e.AccessKeyId == route.AccessKeyId &&
                e.Agent == route.Agent && e.ClientSessionId == route.ClientSessionId && (e.TurnId == route.TurnId || messages.Select(m => m.EventId).Contains(e.EventId)), cancellationToken)) throw Conflict();
            foreach (var message in messages)
                db.SessionCaptureEvents.Add(new SessionCaptureEvent
                {
                    Id = message.Id, DepotId = route.DepotId, AccessKeyId = route.AccessKeyId, WorkspaceId = workspaceId,
                    Agent = route.Agent, ClientSessionId = route.ClientSessionId, TurnId = route.TurnId, Phase = message.Phase,
                    EventId = message.EventId, EncryptedContent = message.EncryptedContent, ContentHash = message.ContentHash,
                    CreatedAt = route.CreatedAt, CaptureExpiresAt = route.ExpiresAt, NextAttemptAt = now
                });
            route.State = "promoted"; route.PromotedAt = now; ClearVector(route); Purge(messages, now);
        }
        await db.SaveChangesAsync(cancellationToken);
        if (route.State == "promoted" && (!options.CurrentValue.Enabled || route.ExpiresAt <= await NowAsync(cancellationToken)))
            return false;
        await transaction.CommitAsync(cancellationToken);
        return route.State == "promoted";
    }

    public async Task<int> CleanupAsync(int limit, CancellationToken cancellationToken)
    {
        var now = await NowAsync(cancellationToken);
        var locators = await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().AsNoTracking().Where(r => r.ExpiresAt <= now &&
            r.State != "promoted" && r.State != "skipped" && r.State != "failed").OrderBy(r => r.ExpiresAt).Take(limit)
            .Select(r => new RouteLocator(r.DepotId, r.AccessKeyId, r.Id)).ToArrayAsync(cancellationToken);
        var count = 0;
        foreach (var locator in locators)
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await TryLockKeyAsync(locator, cancellationToken)) continue;
            var route = await LockRouteAsync(locator, cancellationToken);
            now = await NowAsync(cancellationToken);
            if (route is null || route.State is "promoted" or "skipped" or "failed" || route.ExpiresAt > now) continue;
            var messages = await MessagesAsync(route.Id, cancellationToken);
            Terminate(route, messages, now, "skipped", messages.Length < 2 ? "skipped_incomplete" : "skipped_expired");
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); count++;
        }
        return count;
    }

    private Task<SessionCaptureTurnRoute?> LockRouteAsync(RouteLocator locator, CancellationToken ct) =>
        db.SessionCaptureTurnRoutes.FromSqlInterpolated($"SELECT * FROM public.session_capture_turn_routes WHERE id={locator.RouteId} AND depot_id={locator.DepotId} AND access_key_id={locator.KeyId} FOR UPDATE")
            .IgnoreQueryFilters().SingleOrDefaultAsync(ct);
    private Task<SessionCaptureInboxEvent[]> MessagesAsync(Guid routeId, CancellationToken ct) =>
        db.SessionCaptureInboxEvents.IgnoreQueryFilters().Where(e => e.TurnRouteId == routeId).OrderBy(e => e.Id).ToArrayAsync(ct);
    private Task<DateTimeOffset> NowAsync(CancellationToken ct) => db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
    private Task LockKeyAsync(Guid depot, Guid key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({$"session-capture:{depot}:{key}"},0))", ct);
    private Task<bool> TryLockKeyAsync(RouteLocator locator, CancellationToken ct) => db.Database.SqlQuery<bool>(
        $"SELECT pg_try_advisory_xact_lock(hashtextextended({$"session-capture:{locator.DepotId}:{locator.KeyId}"},0)) AS \"Value\"").SingleAsync(ct);
    private async Task<bool> AuthorizedAsync(Guid depot, Guid key, Guid? workspace, CancellationToken ct) =>
        await db.DepotAccessKeys.IgnoreQueryFilters().AnyAsync(k => k.Id == key && k.DepotId == depot && k.RevokedAt == null &&
            db.WorkspaceAccessGrants.IgnoreQueryFilters().Any(g => g.DepotId == depot && g.DepotAccessKeyId == key && (workspace == null || g.WorkspaceId == workspace)), ct);
    private string Decrypt(SessionCaptureInboxEvent? message)
    {
        if (message?.EncryptedContent is null) throw new CryptographicException();
        var text = protector.Unprotect(message.EncryptedContent);
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))) != message.ContentHash) throw new CryptographicException();
        return text;
    }
    private static bool ValidLease(SessionCaptureTurnRoute? route, RouteLease lease, DateTimeOffset now) => route is not null &&
        route.State == "resolving" && route.LeaseToken == lease.LeaseToken && route.LeaseUntil > now && route.ExpiresAt > now;
    private static void ClearLease(SessionCaptureTurnRoute route) { route.LeaseToken = null; route.LeaseUntil = null; }
    private static void ClearVector(SessionCaptureTurnRoute route)
    { route.EncryptedRoutingVector = null; route.RoutingVectorHash = null; route.RoutingEmbeddingFingerprint = null; }
    private static void Purge(IEnumerable<SessionCaptureInboxEvent> messages, DateTimeOffset now)
    { foreach (var message in messages) { message.EncryptedContent = null; message.ContentPurgedAt = now; } }
    private static void Terminate(SessionCaptureTurnRoute route, IEnumerable<SessionCaptureInboxEvent> messages, DateTimeOffset now, string state, string code)
    { route.State = state; route.ResultCode = code; ClearLease(route); ClearVector(route); Purge(messages, now); }
    private static ContextDepotApplicationException Conflict() => new(ApplicationErrorCodes.SessionCaptureConflict);
    private static ContextDepotApplicationException Unavailable() => new(ApplicationErrorCodes.SessionCaptureUnavailable);
}
