using ContextDepot.Application.DataProtection;
using ContextDepot.Application.DataProtection.Enums;
using ContextDepot.Domain.Inferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Configuration;

public sealed class InferenceRuntimeSnapshotLoader(
    IServiceScopeFactory scopeFactory,
    ISecretProtector apiKeyProtector,
    InferenceRuntimeSnapshotAccessor snapshotAccessor,
    ILogger<InferenceRuntimeSnapshotLoader> logger)
{
    public async Task<InferenceRuntimeSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
        var embeddingRoutes = await db.InferenceRoutes
            .AsNoTracking()
            .Include(route => route.Provider)
            .Where(route => route.Capability == "embedding")
            .ToArrayAsync(cancellationToken);

        if (embeddingRoutes.Length != 1)
        {
            throw new InvalidOperationException("The database must contain exactly one embedding inference route.");
        }

        var snapshot = BuildSnapshot(embeddingRoutes[0]);
        snapshotAccessor.Publish(snapshot);
        if (snapshot.State == InferenceRuntimeState.Ready)
        {
            logger.LogInformation("Loaded the configured embedding inference route from the database.");
        }
        else if (snapshot.State == InferenceRuntimeState.Unconfigured)
        {
            logger.LogWarning("No embedding inference route is configured; semantic retrieval is disabled.");
        }
        else
        {
            logger.LogWarning(
                "The embedding inference route is unavailable ({DegradedReason}); semantic retrieval is disabled.",
                snapshot.DegradedReason);
        }

        return snapshot;
    }

    private InferenceRuntimeSnapshot BuildSnapshot(InferenceRoute route)
    {
        if (route.ProviderId is null && route.ModelName is null)
        {
            if (route.Dimensions is not null || route.EmbeddingProfileFingerprint is not null ||
                !string.Equals(route.IndexState, "unconfigured", StringComparison.Ordinal))
            {
                return Degraded("incomplete-route");
            }

            return new InferenceRuntimeSnapshot(null, InferenceRuntimeState.Unconfigured, null);
        }

        var provider = route.Provider;
        if (route.ProviderId is null || provider is null ||
            string.IsNullOrWhiteSpace(provider.Name) ||
            !string.Equals(provider.ProtocolCode, "openai-compatible", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(route.ModelName) ||
            route.Dimensions is not > 0 ||
            route.TimeoutSeconds is < 1 or > 300 ||
            string.IsNullOrWhiteSpace(route.EmbeddingProfileFingerprint) ||
            !Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            return Degraded("invalid-route");
        }

        string fingerprint;
        try
        {
            fingerprint = EmbeddingProfileFingerprint.Compute(
                provider.Name,
                provider.ProtocolCode,
                provider.BaseUrl,
                route.ModelName,
                route.Dimensions.Value);
        }
        catch (ArgumentException)
        {
            return Degraded("invalid-profile");
        }

        if (!string.Equals(fingerprint, route.EmbeddingProfileFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Degraded("profile-fingerprint-mismatch");
        }

        if (string.IsNullOrWhiteSpace(provider.ProtectedApiKey))
        {
            return Degraded("missing-api-key");
        }

        apiKeyProtector.TryUnprotect(
            provider.ProtectedApiKey,
            SecretProtectionPurpose.InferenceProviderApiKey,
            out var apiKey);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Degraded("api-key-decryption-failed");
        }

        var embedding = new EmbeddingRouteRuntimeSnapshot(
            provider.Name,
            provider.ProtocolCode,
            endpoint,
            apiKey,
            route.ModelName,
            route.Dimensions.Value,
            route.TimeoutSeconds,
            fingerprint);
        return new InferenceRuntimeSnapshot(embedding, InferenceRuntimeState.Ready, null);
    }

    private static InferenceRuntimeSnapshot Degraded(string reason) =>
        new(null, InferenceRuntimeState.Degraded, reason);
}
