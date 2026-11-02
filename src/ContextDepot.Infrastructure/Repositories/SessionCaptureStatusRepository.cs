using System.Data;
using System.Text.Json;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Contexts.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SessionCaptureStatusRepository(ContextDepotDbContext db) : ISessionCaptureStatusRepository
{
    public async Task<SessionCaptureStatusResult?> GetAsync(Guid depotId, Guid keyId, SessionCaptureStatusRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        if (!await db.DepotAccessKeys.IgnoreQueryFilters().AnyAsync(k => k.Id == keyId && k.DepotId == depotId && k.RevokedAt == null, cancellationToken)) return null;
        var grants = await db.WorkspaceAccessGrants.IgnoreQueryFilters().Where(g => g.DepotId == depotId && g.DepotAccessKeyId == keyId)
            .Select(g => g.WorkspaceId).ToArrayAsync(cancellationToken);
        if (grants.Length == 0) return null;
        SessionCaptureTurnRoute? route = null;
        var inbox = db.SessionCaptureInboxEvents.IgnoreQueryFilters().AsNoTracking().Where(e => e.DepotId == depotId && e.AccessKeyId == keyId);
        if (request.TurnReceiptId is Guid turnId)
            route = await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(r => r.Id == turnId && r.DepotId == depotId && r.AccessKeyId == keyId, cancellationToken);
        else if (request.ReceiptId is Guid receiptId)
        {
            var inboxMessage = await inbox.SingleOrDefaultAsync(e => e.Id == receiptId, cancellationToken);
            if (inboxMessage is not null)
                route = await db.SessionCaptureTurnRoutes.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(r => r.Id == inboxMessage.TurnRouteId && r.DepotId == depotId && r.AccessKeyId == keyId, cancellationToken);
        }
        if (request.TurnReceiptId is not null && route is null) return null;
        if (route?.WorkspaceId is Guid routedWorkspace && !grants.Contains(routedWorkspace)) return null;
        var queue = db.SessionCaptureEvents.IgnoreQueryFilters().AsNoTracking().Where(e => e.DepotId == depotId && e.AccessKeyId == keyId);
        SessionCaptureEvent[] messages = [];
        SessionCaptureInboxEvent[] inboxMessages = [];
        Guid? workspaceId;
        if (route is not null)
        {
            inboxMessages = await inbox.Where(e => e.TurnRouteId == route.Id).OrderBy(e => e.Phase).ToArrayAsync(cancellationToken);
            if (route.State == "promoted")
                messages = await queue.Where(e => e.Agent == route.Agent && e.ClientSessionId == route.ClientSessionId && e.TurnId == route.TurnId).OrderBy(e => e.Phase).ToArrayAsync(cancellationToken);
            workspaceId = route.WorkspaceId;
        }
        else
        {
            var message = await queue.SingleOrDefaultAsync(e => e.Id == request.ReceiptId, cancellationToken);
            if (message is null || !grants.Contains(message.WorkspaceId)) return null;
            messages = await queue.Where(e => e.Agent == message.Agent && e.ClientSessionId == message.ClientSessionId && e.TurnId == message.TurnId)
                .OrderBy(e => e.Phase).ToArrayAsync(cancellationToken);
            workspaceId = message.WorkspaceId;
        }
        string? path = null;
        if (workspaceId is Guid target)
        {
            var workspaces = await db.Workspaces.IgnoreQueryFilters().AsNoTracking().Where(w => w.DepotId == depotId).ToArrayAsync(cancellationToken);
            path = new WorkspaceTopology(workspaces.Select(w => new WorkspaceTreeNode(w.Id, w.ParentWorkspaceId, w.Slug))).Paths.GetValueOrDefault(target);
            if (path is null) return null;
        }
        var user = messages.SingleOrDefault(m => m.Phase == "user");
        var createdIds = ReadCreatedIds(user?.CandidateOutcomesJson);
        var visibleIds = await db.ContextItems.IgnoreQueryFilters().Where(c => c.DepotId == depotId && c.WorkspaceId == workspaceId &&
            c.Status == ContextStatus.Active && createdIds.Contains(c.Id)).Select(c => c.Id).ToArrayAsync(cancellationToken);
        var processing = messages.Any(m => m.Status == "processing") ? "processing" : messages.Any(m => m.Status == "pending") ? "pending" :
            messages.Any(m => m.Status == "failed") ? "failed" : messages.Length > 0 ? "completed" : "not_started";
        var terminal = processing is "completed" or "failed";
        var result = route is not null && route.State != "promoted" ? route.ResultCode : terminal ?
            createdIds.Length > 0 ? "saved" : user?.ResultCode ?? messages.FirstOrDefault()?.ResultCode : null;
        var phases = messages.Length > 0 ? messages.Select(m => new SessionCapturePhaseStatus(m.Phase, m.Id, m.EncryptedContent is null)).ToArray() :
            inboxMessages.Select(m => new SessionCapturePhaseStatus(m.Phase, m.Id, m.EncryptedContent is null)).ToArray();
        var response = new SessionCaptureStatusResult(request.ReceiptId, route?.Id, route?.State ?? "not_applicable", processing,
            result, user?.LastErrorCode ?? route?.LastErrorCode, path, phases, visibleIds,
            route?.CreatedAt ?? messages.Min(m => m.CreatedAt), route?.ExpiresAt ?? user?.CaptureExpiresAt,
            processing is "pending" or "processing" ? messages.Min(m => m.NextAttemptAt) : route?.State == "pending" ? route.NextAttemptAt : null,
            route?.AttemptCount ?? 0, user?.AttemptCount ?? 0);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static Guid[] ReadCreatedIds(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Where(e => e.TryGetProperty("outcome", out var outcome) && outcome.GetString() == "created")
            .Select(e => e.TryGetProperty("contextId", out var id) && id.TryGetGuid(out var value) ? value : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
    }
}
