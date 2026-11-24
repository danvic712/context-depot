using System.ClientModel;
using System.Net;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Infrastructure.Access;
using ContextDepot.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed class SessionCaptureCycleRunner(IServiceScopeFactory scopes,
    IOptionsMonitor<SessionCaptureOptions> options, ILogger<SessionCaptureCycleRunner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using (var cleanupScope = InternalScope())
        {
            await cleanupScope.ServiceProvider.GetRequiredService<ISessionCaptureRepository>().CleanupAsync(cancellationToken);
            await cleanupScope.ServiceProvider.GetRequiredService<ISessionCaptureInboxRepository>().CleanupAsync(256, cancellationToken);
        }
        if (!options.CurrentValue.Enabled) return;
        RouteLocator[] promotable;
        await using (var scan = InternalScope())
            promotable = (await scan.ServiceProvider.GetRequiredService<ISessionCaptureInboxRepository>().FindPromotableAsync(16, cancellationToken)).ToArray();
        foreach (var locator in promotable)
        {
            await using var promoteScope = InternalScope();
            await promoteScope.ServiceProvider.GetRequiredService<ISessionCaptureInboxRepository>().PromoteAsync(locator, cancellationToken);
        }
        await ExtractAsync(cancellationToken);
        if (options.CurrentValue.Enabled) await RouteAsync(cancellationToken);
    }

    private async Task ExtractAsync(CancellationToken cancellationToken)
    {
        await using var queueScope = InternalScope();
        var queue = queueScope.ServiceProvider.GetRequiredService<ISessionCaptureRepository>();
        var turn = await queue.ClaimAsync(cancellationToken);
        if (turn is null) return;
        try
        {
            await using var processingScope = scopes.CreateAsyncScope();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.CurrentValue.ExtractionTimeoutSeconds));
            var result = await processingScope.ServiceProvider.GetRequiredService<MemoryCaptureProcessor>().ProcessAsync(turn, timeout.Token);
            await queue.FinishAsync(turn, result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ContextDepotApplicationException exception) when (exception.ErrorCode is ApplicationErrorCodes.WorkspaceUnavailable or ApplicationErrorCodes.SessionCaptureLeaseLost)
        {
            await queue.FinishAsync(turn, new(SessionCaptureDisposition.Completed, "skipped_unauthorized"), cancellationToken);
        }
        catch (Exception exception)
        {
            var disposition = ShouldRetry(exception) ? SessionCaptureDisposition.Retry : SessionCaptureDisposition.Failed;
            logger.LogWarning("Session capture processing failed for receipt {ReceiptId}; disposition={Disposition}.", turn.UserEventId, disposition);
            var code = exception switch
            {
                InvalidOperationException { Message: InfrastructureErrorCodes.SessionMemoryChatUnavailable } => "chat_unavailable",
                System.Text.Json.JsonException => "invalid_extraction_response",
                InvalidOperationException { Message: InfrastructureErrorCodes.SessionMemoryContentUnavailable } => "content_unavailable",
                System.Security.Cryptography.CryptographicException => "content_unavailable",
                ContextDepotApplicationException { ErrorCode: ApplicationErrorCodes.ContextCreateOnlyCapacity } => "capacity_deferred",
                _ => "extraction_failed"
            };
            await queue.FinishAsync(turn, new(disposition, disposition == SessionCaptureDisposition.Retry ? null : code, code), cancellationToken);
        }
    }

    private async Task RouteAsync(CancellationToken cancellationToken)
    {
        RouteLocator[] locators;
        await using (var scan = InternalScope())
            locators = (await scan.ServiceProvider.GetRequiredService<ISessionCaptureInboxRepository>().FindRoutingCandidatesAsync(64, cancellationToken)).ToArray();
        foreach (var locator in locators)
        {
            await using var workScope = InternalScope();
            var inbox = workScope.ServiceProvider.GetRequiredService<ISessionCaptureInboxRepository>();
            var lease = await inbox.TryClaimRouteAsync(locator, cancellationToken);
            if (lease is null) continue;
            RouteDecision decision;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(options.CurrentValue.RoutingTimeoutSeconds));
                var snapshot = await SnapshotAsync(workScope.ServiceProvider.GetRequiredService<ContextDepotDbContext>(), locator, timeout.Token);
                decision = snapshot.Workspaces.Count == 0 && !snapshot.CanCreateRootWorkspaces
                    ? new("permanent_failure", ResultCode: "skipped_unauthorized") :
                    await workScope.ServiceProvider.GetRequiredService<ISessionWorkspaceRouter>().RouteAsync(lease, snapshot, timeout.Token);
                if (decision.Preparation is not null)
                    decision = await workScope.ServiceProvider.GetRequiredService<SessionWorkspacePreparation>()
                        .PrepareAsync(lease, decision, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                var transient = ShouldRetry(exception);
                decision = new(transient ? "transient_failure" : "permanent_failure", ResultCode:
                    exception is System.Text.Json.JsonException ? "invalid_routing_response" : "routing_unavailable");
                logger.LogWarning("Session routing failed for turn receipt {ReceiptId}; transient={Transient}.", locator.RouteId, transient);
            }
            await inbox.CompleteRouteAsync(lease, decision, cancellationToken);
            if (decision.Decision == "resolved") await inbox.PromoteAsync(locator, cancellationToken);
            if (decision.Decision != "budget_deferred") break;
        }
    }

    private static async Task<AuthorizedWorkspaceSnapshot> SnapshotAsync(ContextDepotDbContext db, RouteLocator locator, CancellationToken ct)
    {
        var access = await db.AccessKeys.IgnoreQueryFilters().Where(k => k.Id == locator.KeyId && k.RevokedAt == null)
            .Select(k => new { k.CanCreateRootWorkspaces, Grants = k.WorkspaceGrants.Select(g => g.WorkspaceId).ToArray() })
            .SingleOrDefaultAsync(ct);
        if (access is null) return new(locator.KeyId, []);
        if (access.Grants.Length == 0) return new(locator.KeyId, [], access.CanCreateRootWorkspaces);
        var all = await db.Workspaces.IgnoreQueryFilters().AsNoTracking().ToArrayAsync(ct);
        var paths = new WorkspaceTopology(all.Select(w => new WorkspaceTreeNode(w.Id, w.ParentWorkspaceId, w.Slug))).Paths;
        return new(locator.KeyId, all.Where(w => access.Grants.Contains(w.Id)).OrderBy(w => w.Id).Take(513)
            .Select(w => new RoutingWorkspace(w.Id, w.Name, paths[w.Id], w.Description)).ToArray(), access.CanCreateRootWorkspaces);
    }

    private AsyncServiceScope InternalScope()
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceAccessContext>().AllowInternalAccess();
        return scope;
    }

    private static bool ShouldRetry(Exception exception) => exception switch
    {
        OperationCanceledException => true,
        HttpRequestException httpException => httpException.StatusCode is null || IsTransientStatus((int)httpException.StatusCode.Value),
        ClientResultException providerException => IsTransientStatus(providerException.Status),
        NpgsqlException databaseException => databaseException.IsTransient,
        DbUpdateException { InnerException: NpgsqlException databaseException } => databaseException.IsTransient,
        InvalidOperationException { Message: InfrastructureErrorCodes.SessionMemoryChatUnavailable } => true,
        ContextDepotApplicationException { ErrorCode: ApplicationErrorCodes.ContextCreateOnlyCapacity } => true,
        _ => false
    };

    private static bool IsTransientStatus(int status) => status is (int)HttpStatusCode.RequestTimeout or (int)HttpStatusCode.TooManyRequests || status >= 500;
}
