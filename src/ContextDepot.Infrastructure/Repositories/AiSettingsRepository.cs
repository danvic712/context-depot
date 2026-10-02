using ContextDepot.Application.DataProtection;
using ContextDepot.Application.DataProtection.Enums;
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

public sealed class AiSettingsRepository(ContextDepotDbContext db, ISecretProtector secrets, IIdGenerator ids,
    TimeProvider clock, InferenceRuntimeSnapshotAccessor snapshots, VectorCollectionInitializer collections,
    InferenceRuntimeSnapshotRefresher refresher, ILogger<AiSettingsRepository> logger) : IAiSettingsRepository
{
    public async Task<IReadOnlyList<AiRouteDto>> GetAsync(CancellationToken cancellationToken) =>
        (await db.InferenceRoutes.AsNoTracking().Include(route => route.Provider).OrderBy(route => route.Capability)
            .ToListAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<AiRouteDto> SaveAsync(string capability, SaveAiRouteRequest request, CancellationToken cancellationToken)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var routes = await db.InferenceRoutes.FromSqlInterpolated(
                $"SELECT * FROM public.inference_routes WHERE capability = {capability} FOR UPDATE").ToListAsync(cancellationToken);
            var route = routes.Single();
            await db.Entry(route).Reference(item => item.Provider).LoadAsync(cancellationToken);
            if (route.UpdatedAt != request.UpdatedAt)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
            var protectedKey = request.ApiKey is null ? route.Provider?.ProtectedApiKey
                : secrets.Protect(request.ApiKey, SecretProtectionPurpose.InferenceProviderApiKey);
            if (string.IsNullOrWhiteSpace(protectedKey) ||
                !secrets.TryUnprotect(protectedKey, SecretProtectionPurpose.InferenceProviderApiKey, out var apiKey) ||
                string.IsNullOrWhiteSpace(apiKey))
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidAiConfiguration);
            var fingerprint = capability == "embedding"
                ? EmbeddingProfileFingerprint.Compute(request.ProviderName, "openai-compatible", request.Endpoint, request.Model, request.Dimensions!.Value)
                : null;
            // Prepare isolated derived collections before committing. A failure leaves the old route active.
            if (capability == "embedding")
                await collections.InitializeAsync(new EmbeddingRouteRuntimeSnapshot(request.ProviderName, "openai-compatible",
                    new Uri(request.Endpoint), apiKey, request.Model, request.Dimensions!.Value, request.TimeoutSeconds, fingerprint!), cancellationToken);
            var now = clock.GetUtcNow();
            var provider = route.Provider;
            if (provider is null || await db.InferenceRoutes.AnyAsync(other => other.Id != route.Id && other.ProviderId == provider.Id, cancellationToken))
            {
                provider = new InferenceProvider { Id = ids.NewId(), CreatedAt = now };
                db.InferenceProviders.Add(provider);
            }
            provider.Name = request.ProviderName;
            provider.ProtocolCode = "openai-compatible";
            provider.BaseUrl = request.Endpoint;
            provider.ProtectedApiKey = protectedKey;
            provider.VerificationState = "unverified";
            provider.UpdatedAt = now;
            route.Provider = provider;
            route.ProviderId = provider.Id;
            route.ModelName = request.Model;
            route.Dimensions = request.Dimensions;
            route.TimeoutSeconds = request.TimeoutSeconds;
            if (route.EmbeddingProfileFingerprint != fingerprint)
            {
                route.IndexGeneration++;
                route.IndexState = capability == "embedding" ? "pending" : "not-applicable";
            }
            route.EmbeddingProfileFingerprint = fingerprint;
            route.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (capability == "embedding")
        {
            try { await refresher.RefreshAsync(cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The reload service will retry; the DTO explicitly reports pending activation.
                logger.LogWarning("Embedding settings were saved but activation is pending. Error type: {ErrorType}", exception.GetType().Name);
            }
        }
        return (await GetAsync(cancellationToken)).Single(route => route.Capability == capability);
    }

    // Old database rows may predate the editor's URL validation. Never return URL credentials.
    private static string? PublicEndpoint(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("http" or "https")) return null;
        return new UriBuilder(endpoint) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.AbsoluteUri;
    }

    private AiRouteDto ToDto(InferenceRoute route)
    {
        var snapshot = snapshots.Current;
        var applied = route.Capability == "embedding" && snapshot.State == InferenceRuntimeState.Ready &&
            snapshot.Embedding is not null && snapshot.Embedding.ProfileFingerprint == route.EmbeddingProfileFingerprint &&
            snapshot.Embedding.TimeoutSeconds == route.TimeoutSeconds &&
            secrets.TryUnprotect(route.Provider?.ProtectedApiKey ?? "", SecretProtectionPurpose.InferenceProviderApiKey, out var key) &&
            snapshot.Embedding.ApiKey == key;
        var state = route.Capability == "chat" ? (route.ProviderId is null ? "unconfigured" : "configured")
            : applied ? "active" : route.ProviderId is null ? "unconfigured" : "pending";
        return new AiRouteDto(route.Capability, route.Provider?.Name, "openai-compatible", PublicEndpoint(route.Provider?.BaseUrl),
            route.ModelName, route.Dimensions, route.TimeoutSeconds, !string.IsNullOrWhiteSpace(route.Provider?.ProtectedApiKey),
            route.UpdatedAt, state, route.IndexState, applied);
    }
}
