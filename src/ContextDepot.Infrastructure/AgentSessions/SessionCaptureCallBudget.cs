using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.AgentSessions;

/// <summary>Reserves exactly one external provider request, shared by routing and extraction.</summary>
public sealed class SessionCaptureCallBudget(ContextDepotDbContext db, IOptionsMonitor<SessionCaptureOptions> options) : ISessionCaptureCallBudget
{
    public async Task<BudgetReservation> TryReserveAsync(CaptureWorkLease lease, ProviderCallKind kind, CancellationToken cancellationToken)
    {
        if (!((lease.OwnerKind == "route" && kind is ProviderCallKind.RoutingEmbedding or ProviderCallKind.RoutingChat) ||
            (lease.OwnerKind == "extraction" && kind == ProviderCallKind.ExtractionChat))) return new("lease_lost");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SessionCaptureLocks.LockKeyAsync(db, lease.DepotId, lease.KeyId, cancellationToken);
        var route = lease.OwnerKind == "route" ? (await db.SessionCaptureTurnRoutes.FromSqlInterpolated(
            $"SELECT * FROM public.session_capture_turn_routes WHERE id = {lease.OwnerId} FOR UPDATE")
            .IgnoreQueryFilters().ToArrayAsync(cancellationToken)).SingleOrDefault() : null;
        var message = lease.OwnerKind == "extraction" ? (await db.SessionCaptureEvents.FromSqlInterpolated(
            $"SELECT * FROM public.session_capture_events WHERE id = {lease.OwnerId} FOR UPDATE")
            .IgnoreQueryFilters().ToArrayAsync(cancellationToken)).SingleOrDefault() : null;
        if (route is not null) await db.Entry(route).ReloadAsync(cancellationToken);
        if (message is not null) await db.Entry(message).ReloadAsync(cancellationToken);
        try
        {
            if (message is not null) await SessionAccessGuard.EnsureAsync(db, lease.DepotId, lease.KeyId, message.WorkspaceId, cancellationToken);
            else
            {
                await SessionAccessGuard.EnsureAnyAsync(db, lease.DepotId, lease.KeyId, cancellationToken);
            }
        }
        catch (ContextDepotApplicationException) { return new("lease_lost"); }
        // Authorization row locks can wait beyond the lease/deadline or a settings change.
        // Validate against database time and settings only after every blocking lock is held.
        var now = await SessionCaptureLocks.NowAsync(db, cancellationToken);
        var settings = options.CurrentValue;
        if (!settings.Enabled) return new("lease_lost");
        var expires = route?.ExpiresAt ?? message?.CaptureExpiresAt ?? message?.CreatedAt.AddHours(settings.RetentionHours);
        if (expires is not null && expires <= now) return new("expired");
        if (route is not null)
        {
            if (route.DepotId != lease.DepotId || route.AccessKeyId != lease.KeyId || route.State != "resolving" ||
                route.LeaseToken != lease.LeaseToken || route.LeaseUntil == null || route.LeaseUntil <= now) return new("lease_lost");
        }
        else if (message is null || message.DepotId != lease.DepotId || message.AccessKeyId != lease.KeyId ||
            message.Phase != "user" || message.Status != "processing" || message.LeaseToken != lease.LeaseToken ||
            message.LeaseUntil is null || message.LeaseUntil <= now || message.EncryptedExtractionPlan != null ||
            message.AttemptCount >= settings.MaxAttempts) return new("lease_lost");
        var until = await FindWaitUntilAsync(db, lease.DepotId, lease.KeyId, settings.MaxCallsPerHour, now, cancellationToken);
        if (until is not null)
        {
            if (expires <= until) return new("expired");
            if (message is not null)
            {
                // Persist the precise boundary for Finish; waiting consumes neither a call nor an attempt.
                message.NextAttemptAt = until.Value;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            return new("wait", until);
        }
        if (route is not null) { route.ProviderCallCount++; route.LastProviderCallAt = now; }
        else { message!.ProviderCallCount++; message.LastProviderCallAt = now; message.AttemptCount++; }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new("granted");
    }
    internal static async Task<DateTimeOffset?> FindWaitUntilAsync(ContextDepotDbContext db, Guid depotId, Guid keyId,
        int maxCalls, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now.AddHours(-1);
        var routes = await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().Where(x => x.DepotId == depotId &&
            x.AccessKeyId == keyId && x.LastProviderCallAt >= since && x.ProviderCallCount > 0)
            .Select(x => new { Count = x.ProviderCallCount, At = x.LastProviderCallAt!.Value }).ToArrayAsync(cancellationToken);
        var events = await db.SessionCaptureEvents.IgnoreQueryFilters().Where(x => x.DepotId == depotId &&
            x.AccessKeyId == keyId && x.Phase == "user" && x.LastProviderCallAt >= since && x.ProviderCallCount > 0)
            .Select(x => new { Count = x.ProviderCallCount, At = x.LastProviderCallAt!.Value }).ToArrayAsync(cancellationToken);
        var contributors = routes.Concat(events).ToArray();
        return contributors.Sum(x => x.Count) < maxCalls ? null :
            contributors.Length == 0 ? now.AddHours(1) : contributors.Min(x => x.At).AddHours(1).AddMilliseconds(1);
    }

}
