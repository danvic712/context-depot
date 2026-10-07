using ContextDepot.Application.DataProtection;
using ContextDepot.Application.DataProtection.Enums;
using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Domain.Inferences;
using ContextDepot.Infrastructure.Embeddings;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class InferenceSettingsRepository(ContextDepotDbContext db, ISecretProtector secrets, IIdGenerator ids,
    TimeProvider clock, InferenceRuntimeSnapshotAccessor snapshots, VectorCollectionInitializer collections,
    InferenceRuntimeSnapshotRefresher refresher, ILogger<InferenceSettingsRepository> logger) : IInferenceSettingsRepository
{
    public async Task<IReadOnlyList<InferenceRouteDto>> GetAsync(CancellationToken cancellationToken) =>
        (await db.InferenceRoutes.AsNoTracking().Include(route => route.Provider).OrderBy(route => route.Capability)
            .ToListAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<InferenceProviderSettingsDto> GetProvidersAsync(CancellationToken cancellationToken)
    {
        var providers = await db.InferenceProviders.AsNoTracking().OrderBy(provider => provider.Name)
            .ToArrayAsync(cancellationToken);
        return new(providers.Select(provider => new InferenceProviderDto(provider.Id, provider.Name, provider.ProtocolCode,
            InferenceConfigurationValidator.PublicEndpoint(provider.BaseUrl), !string.IsNullOrWhiteSpace(provider.ProtectedApiKey), provider.UpdatedAt, provider.Kind)).ToArray(),
            await GetAsync(cancellationToken));
    }

    public async Task<InferenceProviderSettingsDto> SaveProviderAsync(SaveInferenceProviderRequest request, CancellationToken cancellationToken)
    {
        var previous = request.Id is Guid id
            ? await db.InferenceProviders.AsNoTracking().SingleOrDefaultAsync(provider => provider.Id == id, cancellationToken)
            : null;
        if (request.Id is not null && (previous is null || previous.UpdatedAt != request.UpdatedAt))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
        if (previous is not null && previous.Kind != request.Kind)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
        var protectedKey = request.ApiKey is null ? previous?.ProtectedApiKey
            : secrets.Protect(request.ApiKey, SecretProtectionPurpose.InferenceProviderApiKey);
        if (string.IsNullOrWhiteSpace(protectedKey) ||
            !secrets.TryUnprotect(protectedKey, SecretProtectionPurpose.InferenceProviderApiKey, out var apiKey) ||
            string.IsNullOrWhiteSpace(apiKey))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
        var fingerprint = request.Embedding is { } model
            ? EmbeddingProfileFingerprint.Compute(request.Name, "openai-compatible", request.Endpoint, model.Model, model.Dimensions!.Value)
            : null;
        // Prepare derived storage before taking row locks. Recheck every revision before committing.
        if (request.Embedding is { } embedding)
            await collections.InitializeAsync(new EmbeddingRouteRuntimeSnapshot(request.Name, "openai-compatible",
                new Uri(request.Endpoint), apiKey, embedding.Model, embedding.Dimensions!.Value,
                embedding.TimeoutSeconds, fingerprint!), cancellationToken);
        bool refreshEmbedding;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var routes = await db.InferenceRoutes.FromSqlRaw(
                "SELECT * FROM public.inference_routes ORDER BY capability FOR UPDATE").ToArrayAsync(cancellationToken);
            var embeddingRoute = routes.Single(route => route.Capability == "embedding");
            var chatRoute = routes.Single(route => route.Capability == "chat");
            if (embeddingRoute.UpdatedAt != request.EmbeddingUpdatedAt || chatRoute.UpdatedAt != request.ChatUpdatedAt)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
            var provider = request.Id is Guid providerId
                ? await db.InferenceProviders.FromSqlInterpolated(
                    $"SELECT * FROM public.inference_providers WHERE id = {providerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
                : null;
            if (request.Id is not null && (provider is null || provider.UpdatedAt != request.UpdatedAt))
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
            var now = clock.GetUtcNow();
            if (provider is null)
            {
                provider = new InferenceProvider { Id = ids.NewId(), CreatedAt = now };
                db.InferenceProviders.Add(provider);
            }
            refreshEmbedding = request.Embedding is not null || embeddingRoute.ProviderId == provider.Id;
            provider.Name = request.Name;
            provider.Kind = request.Kind;
            provider.ProtocolCode = "openai-compatible";
            provider.BaseUrl = request.Endpoint;
            provider.ProtectedApiKey = protectedKey;
            provider.VerificationState = "unverified";
            provider.UpdatedAt = now;
            AssignModel(embeddingRoute, request.Embedding, provider, fingerprint, now);
            AssignModel(chatRoute, request.Chat, provider, null, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (refreshEmbedding)
        {
            try { await refresher.RefreshAsync(cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Provider settings were saved but embedding activation is pending. Error type: {ErrorType}", exception.GetType().Name);
            }
        }
        return await GetProvidersAsync(cancellationToken);
    }

    private static void AssignModel(InferenceRoute route, InferenceProviderModelRequest? model,
        InferenceProvider provider, string? fingerprint, DateTimeOffset now)
    {
        if (model is null && route.ProviderId != provider.Id) return;
        route.Provider = model is null ? null : provider;
        route.ProviderId = model is null ? null : provider.Id;
        route.ModelName = model?.Model;
        route.Dimensions = model?.Dimensions;
        if (model is not null) route.TimeoutSeconds = model.TimeoutSeconds;
        if (route.EmbeddingProfileFingerprint != fingerprint)
        {
            route.IndexGeneration++;
            route.IndexState = model is null ? "unconfigured" : "pending";
        }
        if (route.Capability == "chat") route.IndexState = "not-applicable";
        route.EmbeddingProfileFingerprint = fingerprint;
        route.UpdatedAt = now;
    }

    public async Task<InferenceRouteDto> SaveAsync(string capability, SaveInferenceRouteRequest request, CancellationToken cancellationToken)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var route = await db.InferenceRoutes.FromSqlInterpolated(
                $"SELECT * FROM public.inference_routes WHERE capability = {capability} FOR UPDATE").SingleAsync(cancellationToken);
            if (route.UpdatedAt != request.UpdatedAt)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
            InferenceProvider? provider = null;
            string? fingerprint = null;
            if (request.ProviderId is Guid providerId)
            {
                provider = await db.InferenceProviders.FromSqlInterpolated(
                    $"SELECT * FROM public.inference_providers WHERE id = {providerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
                if (provider is null || provider.UpdatedAt != request.ProviderUpdatedAt)
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
                if (!InferenceProviderKinds.IsSupported(provider.Kind) ||
                    (capability == "embedding" && !InferenceProviderKinds.SupportsEmbedding(provider.Kind)) ||
                    !Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var endpoint) ||
                    endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo) ||
                    !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) ||
                    !InferenceProviderKinds.IsValidEndpoint(provider.Kind, endpoint) ||
                    !secrets.TryUnprotect(provider.ProtectedApiKey ?? "", SecretProtectionPurpose.InferenceProviderApiKey, out var apiKey) ||
                    string.IsNullOrWhiteSpace(apiKey))
                    throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidInferenceConfiguration);
                if (capability == "embedding")
                {
                    fingerprint = EmbeddingProfileFingerprint.Compute(provider.Name, provider.ProtocolCode, provider.BaseUrl,
                        request.Model!, request.Dimensions!.Value);
                    // Prepare the selected profile before committing; the other capability stays unchanged.
                    await collections.InitializeAsync(new EmbeddingRouteRuntimeSnapshot(provider.Name, provider.ProtocolCode,
                        endpoint, apiKey, request.Model!, request.Dimensions.Value, request.TimeoutSeconds, fingerprint), cancellationToken);
                }
            }
            route.Provider = provider;
            route.ProviderId = provider?.Id;
            route.ModelName = request.Model;
            route.Dimensions = request.Dimensions;
            route.TimeoutSeconds = request.TimeoutSeconds;
            if (route.EmbeddingProfileFingerprint != fingerprint)
            {
                route.IndexGeneration++;
                route.IndexState = provider is null ? "unconfigured" : "pending";
            }
            if (capability == "chat") route.IndexState = "not-applicable";
            route.EmbeddingProfileFingerprint = fingerprint;
            route.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (capability == "embedding")
        {
            try { await refresher.RefreshAsync(cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Embedding settings were saved but activation is pending. Error type: {ErrorType}", exception.GetType().Name);
            }
        }
        return (await GetAsync(cancellationToken)).Single(route => route.Capability == capability);
    }

    private InferenceRouteDto ToDto(InferenceRoute route)
    {
        var snapshot = snapshots.Current;
        var applied = route.Capability == "embedding" && snapshot.State == InferenceRuntimeState.Ready &&
            snapshot.Embedding is not null && snapshot.Embedding.ProfileFingerprint == route.EmbeddingProfileFingerprint &&
            snapshot.Embedding.TimeoutSeconds == route.TimeoutSeconds &&
            secrets.TryUnprotect(route.Provider?.ProtectedApiKey ?? "", SecretProtectionPurpose.InferenceProviderApiKey, out var key) &&
            snapshot.Embedding.ApiKey == key;
        var state = route.Capability == "chat" ? (route.ProviderId is null ? "unconfigured" : "configured")
            : applied ? "active" : route.ProviderId is null ? "unconfigured" : "pending";
        return new InferenceRouteDto(route.Capability, route.Provider?.Name, "openai-compatible", InferenceConfigurationValidator.PublicEndpoint(route.Provider?.BaseUrl),
            route.ModelName, route.Dimensions, route.TimeoutSeconds, !string.IsNullOrWhiteSpace(route.Provider?.ProtectedApiKey),
            route.UpdatedAt, state, route.IndexState, applied, route.ProviderId);
    }
}
