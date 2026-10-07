using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Embeddings;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SetupRepository(ContextDepotDbContext db, InferenceRuntimeSnapshotRefresher? refresher = null,
    ILogger<SetupRepository>? logger = null) : ISetupRepository
{
    public async Task<SetupSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var state = await db.InstallationStates.AsNoTracking().SingleOrDefaultAsync(
            state => state.Scope == InstallationState.InstallationScope, cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupUnavailable);
        return await LoadSnapshotAsync(state, cancellationToken);
    }

    public async Task<InferenceProviderSettingsDto> GetInferenceSettingsAsync(CancellationToken cancellationToken)
    {
        var providers = await db.InferenceProviders.AsNoTracking().OrderBy(provider => provider.Name).ToArrayAsync(cancellationToken);
        var routes = await db.InferenceRoutes.AsNoTracking().Include(route => route.Provider).OrderBy(route => route.Capability).ToArrayAsync(cancellationToken);
        return new(providers.Select(provider => new InferenceProviderDto(provider.Id, provider.Name, provider.ProtocolCode,
            InferenceConfigurationValidator.PublicEndpoint(provider.BaseUrl), !string.IsNullOrWhiteSpace(provider.ProtectedApiKey), provider.UpdatedAt, provider.Kind)).ToArray(),
            routes.Select(route => new InferenceRouteDto(route.Capability, route.Provider?.Name, "openai-compatible",
                InferenceConfigurationValidator.PublicEndpoint(route.Provider?.BaseUrl), route.ModelName, route.Dimensions, route.TimeoutSeconds,
                !string.IsNullOrWhiteSpace(route.Provider?.ProtectedApiKey), route.UpdatedAt, route.ProviderId is null ? "unconfigured" : "configured",
                route.IndexState, false, route.ProviderId)).ToArray());
    }

    public async Task<SetupSnapshot> MutateAsync(Func<SetupSnapshot, SetupResources?> mutation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var scope = InstallationState.InstallationScope;
        var state = await db.InstallationStates.FromSqlInterpolated(
            $"SELECT * FROM public.installation_state WHERE scope = {scope} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupUnavailable);
        await db.Entry(state).ReloadAsync(cancellationToken);
        SetupResources? resources = null;
        SetupSnapshot snapshot;
        try
        {
            snapshot = await LoadSnapshotAsync(state, cancellationToken);
            if (state.State != InstallationState.Completed)
            {
                // Consistent lock order with the settings editor; no intermediate write takes place.
                var routes = await db.InferenceRoutes.FromSqlRaw("SELECT * FROM public.inference_routes ORDER BY capability FOR UPDATE").ToArrayAsync(cancellationToken);
                var providers = await db.InferenceProviders.FromSqlRaw("SELECT * FROM public.inference_providers ORDER BY id FOR UPDATE").ToArrayAsync(cancellationToken);
                foreach (var entry in routes) await db.Entry(entry).ReloadAsync(cancellationToken);
                foreach (var entry in providers) await db.Entry(entry).ReloadAsync(cancellationToken);
                snapshot = snapshot with { Routes = routes, Providers = providers };
            }
            resources = mutation(snapshot);
            if (resources is not null)
            {
                if (!resources.ExistingWorkspace)
                {
                    db.Depots.Add(resources.Depot);
                    db.Workspaces.Add(resources.Workspace);
                }
                else
                {
                    var tracked = db.Workspaces.Local.SingleOrDefault(item => item.Id == resources.Workspace.Id);
                    if (tracked is not null) db.Entry(tracked).CurrentValues.SetValues(resources.Workspace);
                    else
                    {
                        db.Workspaces.Attach(resources.Workspace);
                        db.Entry(resources.Workspace).Property(item => item.Name).IsModified = true;
                        db.Entry(resources.Workspace).Property(item => item.Slug).IsModified = true;
                        db.Entry(resources.Workspace).Property(item => item.Description).IsModified = true;
                        db.Entry(resources.Workspace).Property(item => item.UpdatedAt).IsModified = true;
                    }
                }
                foreach (var provider in resources.Providers)
                    if (db.Entry(provider).State == EntityState.Detached) db.InferenceProviders.Add(provider);
                foreach (var route in resources.Routes)
                {
                    var fingerprint = route.Capability == "embedding" && route.Provider is { } provider
                        ? EmbeddingProfileFingerprint.Compute(provider.Name, provider.ProtocolCode, provider.BaseUrl, route.ModelName!, route.Dimensions!.Value) : null;
                    if (route.EmbeddingProfileFingerprint != fingerprint)
                    {
                        route.IndexGeneration++;
                        route.IndexState = fingerprint is null ? "unconfigured" : "pending";
                    }
                    if (route.Capability == "chat") route.IndexState = "not-applicable";
                    route.EmbeddingProfileFingerprint = fingerprint;
                }
                if (resources.AccessKey is not null) db.DepotAccessKeys.Add(resources.AccessKey);
                snapshot = snapshot with { Workspace = resources.Workspace, InitialDepotExists = true, HasAnyDepot = true };
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Rolled-back values must not be committed by a later operation in this request scope.
            foreach (var entry in db.ChangeTracker.Entries().Where(entry => entry.Entity is InstallationState ||
                entry.Entity is Depot or Workspace or InferenceProvider or InferenceRoute or DepotAccessKey or WorkspaceAccessGrant &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
                entry.State = EntityState.Detached;
            throw;
        }
        // Publish runtime configuration only after the complete configuration transaction has committed.
        if (resources is not null && refresher is not null)
        {
            try { await refresher.RefreshAsync(cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { logger?.LogWarning("Setup configuration is saved; model activation is pending. Error type: {ErrorType}", exception.GetType().Name); }
        }
        return snapshot;
    }

    private async Task<SetupSnapshot> LoadSnapshotAsync(InstallationState state, CancellationToken cancellationToken)
    {
        var workspace = state.InitialWorkspaceId is Guid workspaceId && state.InitialDepotId is Guid depotId
            ? await db.Workspaces.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
                workspace => workspace.Id == workspaceId && workspace.DepotId == depotId, cancellationToken) : null;
        var initialDepotExists = state.InitialDepotId is Guid initialDepotId &&
            await db.Depots.AsNoTracking().AnyAsync(depot => depot.Id == initialDepotId, cancellationToken);
        return new(state, workspace, initialDepotExists, initialDepotExists || await db.Depots.AsNoTracking().AnyAsync(cancellationToken));
    }
}
