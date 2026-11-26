using System.Text;
using System.Text.RegularExpressions;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Access;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.AgentSessions;

public sealed partial class SessionWorkspacePreparation(ContextDepotDbContext db, IServiceScopeFactory scopes,
    ISessionCaptureCallBudget budget, IMemoryExtractionService extraction, ISecretDetector secrets,
    IOptionsMonitor<SessionCaptureOptions> options)
{
    public async Task<RouteDecision> PrepareAsync(RouteLease lease, RouteDecision decision, CancellationToken cancellationToken)
    {
        if (lease.AssistantContent is null || decision.Preparation is not { } preparation)
            return new("permanent_failure", ResultCode: "invalid_routing_response");

        var remaining = lease.LeaseUntil - await SessionCaptureLocks.NowAsync(db, cancellationToken);
        if (remaining <= TimeSpan.Zero) return new("transient_failure", ResultCode: "lease_lost");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(remaining.TotalSeconds, options.CurrentValue.ExtractionTimeoutSeconds)));
        var ct = timeout.Token;

        var locator = lease.Locator;
        var reservation = await budget.TryReserveAsync(new(locator.KeyId, "route", locator.RouteId,
            lease.LeaseToken), ProviderCallKind.ExtractionChat, ct);
        if (reservation.Status != "granted") return SessionRoutingCandidateRepository.ReservationDecision(reservation);
        var candidates = await extraction.ExtractAsync(lease.UserContent, lease.AssistantContent, ct);
        var plan = new ExtractionPlanValidator(secrets).Validate(candidates, lease.UserContent);
        if (plan.Candidates.Count == 0) return new("no_memory", ResolverVersion: decision.ResolverVersion);
        if (!plan.Candidates.Any(candidate => candidate.Evidence.Any(fragment =>
                RoutingCandidatePolicy.ContainsExact(fragment.Text, preparation.Name))))
            return new("no_match", ResolverVersion: decision.ResolverVersion);

        var key = await db.AccessKeys.IgnoreQueryFilters().AsNoTracking()
            .Include(item => item.WorkspaceGrants).SingleOrDefaultAsync(item => item.Id == locator.KeyId && item.RevokedAt == null, ct);
        if (key is null) return Unavailable();
        var grants = key.WorkspaceGrants.Select(item => item.WorkspaceId).ToArray();
        if (preparation.ParentId is Guid requestedParent ? !grants.Contains(requestedParent) : !key.CanCreateRootWorkspaces)
            return Unavailable();

        // Run Ensure as the capture key. The writer checks the live creation permission and handles concurrent writes.
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceAccessContext>().Initialize(new(key.Id, grants, grants, key.CanCreateRootWorkspaces, key.AccessVersion));
        var workspaces = scope.ServiceProvider.GetRequiredService<IWorkspaceAppService>();
        var slug = ProjectSlug(preparation.Name);
        var path = slug;
        if (preparation.ParentId is Guid parentId)
        {
            var parent = await workspaces.GetAsync(parentId, ct);
            if (parent is null) return Unavailable();
            path = $"{parent.Path}/{slug}";
        }
        var now = await SessionCaptureLocks.NowAsync(db, ct);
        if (!options.CurrentValue.Enabled || now >= lease.ExpiresAt || !await db.SessionCaptureTurnRoutes.IgnoreQueryFilters()
                .AnyAsync(route => route.Id == locator.RouteId &&
                    route.AccessKeyId == locator.KeyId && route.State == "resolving" &&
                    route.LeaseToken == lease.LeaseToken && route.LeaseUntil > now, ct))
            return new("transient_failure", ResultCode: "lease_lost");

        try
        {
            var workspace = await workspaces.EnsureAsync(new(new(WorkspaceSubjectType.Project, slug),
                path, preparation.Name), ct);
            return new("resolved", workspace.Workspace.Id, ResolverVersion: decision.ResolverVersion, Plan: plan);
        }
        catch (ContextDepotApplicationException exception) when (exception.ErrorCode == ApplicationErrorCodes.WorkspaceUnavailable)
        {
            return Unavailable();
        }
        catch (ContextDepotApplicationException exception) when (exception.ErrorCode == ApplicationErrorCodes.WorkspaceConcurrencyConflict)
        {
            return new("transient_failure", ResultCode: "workspace_concurrency_conflict");
        }
    }

    private static RouteDecision Unavailable() => new("permanent_failure", ResultCode: "workspace_unavailable");

    private static string ProjectSlug(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormC);
        if (SlugSource().IsMatch(normalized))
        {
            var slug = SlugSeparator().Replace(CamelBoundary().Replace(normalized, "$1-$2").ToLowerInvariant(), "-").Trim('-');
            if (slug.Length <= 100) return slug;
        }
        return $"project-{CapturePlanCodec.Hash(normalized)[..20].ToLowerInvariant()}";
    }

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._ -]*$")]
    private static partial Regex SlugSource();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugSeparator();
}
