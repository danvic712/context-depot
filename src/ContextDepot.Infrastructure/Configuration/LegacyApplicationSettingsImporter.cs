using System.Globalization;
using System.Text.Json;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Infrastructure.Configuration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContextDepot.Infrastructure;

public sealed class LegacyApplicationSettingsImporter(
    ContextDepotDbContext db,
    DatabaseApplicationSettingsSnapshotBuilder snapshotBuilder,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    IInferenceApiKeyProtector apiKeyProtector)
{
    private static readonly IReadOnlyDictionary<string, string> SeedValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContextDepot:Retrieval:Semantic:ScopeTopK"] = "8",
            ["ContextDepot:Retrieval:Semantic:CandidateTopKPerSource"] = "20",
            ["ContextDepot:Retrieval:Semantic:OversampleFactor"] = "3",
            ["ContextDepot:Retrieval:Semantic:RetrievalLexicalFallbackThreshold"] = "0.65",
            ["ContextDepot:Retrieval:Semantic:DedupSimilarityThreshold"] = "0.98",
            ["ContextDepot:Retrieval:Semantic:DedupTokenOverlapThreshold"] = "0.8",
            ["ContextDepot:Retrieval:Search:DefaultLimit"] = "10",
            ["ContextDepot:Retrieval:Search:MaxLimit"] = "50",
            ["ContextDepot:IndexRepair:PollIntervalSeconds"] = "30",
            ["ContextDepot:IndexRepair:BatchSize"] = "32",
            ["ContextDepot:IndexRepair:MaxBatchesPerCycle"] = "4",
            ["ContextDepot:VectorCoverage:CacheDurationSeconds"] = "30",
            ["ContextDepot:Appearance:Language"] = "\"zh-CN\"",
            ["ContextDepot:Appearance:Theme"] = "\"system\""
        };

    public async Task<LegacyConfigurationImportResult> ImportAsync(
        IConfiguration legacyConfiguration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(legacyConfiguration);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var records = await db.ApplicationSettings.ToListAsync(cancellationToken);
        var byKey = records.ToDictionary(record => record.Key, StringComparer.OrdinalIgnoreCase);
        var importedCount = 0;

        foreach (var (key, seededValue) in SeedValues)
        {
            var legacyValue = ReadLegacyValue(legacyConfiguration, key);
            if (legacyValue is null)
            {
                continue;
            }

            if (!byKey.TryGetValue(key, out var record))
            {
                throw new InvalidOperationException(
                    $"Application setting '{key}' is missing from the database. Apply migrations before importing.");
            }

            var jsonValue = ConvertToJsonScalar(key, legacyValue);
            if (string.Equals(record.ValueJson, jsonValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(record.ValueJson, seededValue, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Application setting '{key}' already contains a non-seed value; refusing to overwrite it.");
            }

            record.ValueJson = jsonValue;
            record.UpdatedAt = timeProvider.GetUtcNow();
            importedCount++;
        }

        _ = snapshotBuilder.Build(records);
        var embeddingImport = await ImportEmbeddingRouteAsync(legacyConfiguration, cancellationToken);
        if (importedCount > 0 || embeddingImport.Imported)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new LegacyConfigurationImportResult(
            importedCount,
            embeddingImport.Imported,
            embeddingImport.Configured);
    }

    private async Task<EmbeddingRouteImportResult> ImportEmbeddingRouteAsync(
        IConfiguration legacyConfiguration,
        CancellationToken cancellationToken)
    {
        var adapter = legacyConfiguration["ContextDepot:Embedding:Adapter"];
        if (string.IsNullOrWhiteSpace(adapter) ||
            string.Equals(adapter, "None", StringComparison.OrdinalIgnoreCase))
        {
            return new EmbeddingRouteImportResult(false, false);
        }

        if (!string.Equals(adapter, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The legacy embedding adapter is not supported for import.");
        }

        var providerName = legacyConfiguration["ContextDepot:Embedding:Provider"];
        var modelName = legacyConfiguration["ContextDepot:Embedding:Model"];
        var endpointValue = legacyConfiguration["ContextDepot:Embedding:OpenAICompatible:Endpoint"];
        var apiKey = legacyConfiguration["ContextDepot:Embedding:OpenAICompatible:ApiKey"];
        var dimensionsValue = legacyConfiguration["ContextDepot:Embedding:Dimensions"] ?? "1536";
        var timeoutValue = legacyConfiguration["ContextDepot:Embedding:OpenAICompatible:TimeoutSeconds"];

        if (string.IsNullOrWhiteSpace(providerName) ||
            string.Equals(providerName.Trim(), "ConfiguredGenerator", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(modelName) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            !Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") ||
            !int.TryParse(dimensionsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimensions) ||
            dimensions <= 0 ||
            !int.TryParse(timeoutValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutSeconds) ||
            timeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                "The active legacy embedding provider, endpoint, model, dimensions, timeout and API key must be valid before import.");
        }

        var fingerprint = EmbeddingProfileFingerprint.Compute(
            providerName,
            "openai-compatible",
            endpointValue,
            modelName,
            dimensions);
        var route = await db.InferenceRoutes
            .Include(candidate => candidate.Provider)
            .SingleOrDefaultAsync(candidate => candidate.Capability == "embedding", cancellationToken)
            ?? throw new InvalidOperationException("The embedding inference route is missing. Apply migrations before importing.");

        if (route.ProviderId is not null || route.ModelName is not null)
        {
            if (route.Provider is null ||
                !string.Equals(route.Provider.Name, providerName, StringComparison.Ordinal) ||
                !string.Equals(route.Provider.ProtocolCode, "openai-compatible", StringComparison.Ordinal) ||
                !string.Equals(route.Provider.BaseUrl, endpointValue, StringComparison.Ordinal) ||
                !string.Equals(route.ModelName, modelName, StringComparison.Ordinal) ||
                route.Dimensions != dimensions ||
                route.TimeoutSeconds != timeoutSeconds ||
                !string.Equals(route.EmbeddingProfileFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(route.Provider.ProtectedApiKey) ||
                !string.Equals(apiKeyProtector.Unprotect(route.Provider.ProtectedApiKey), apiKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The database embedding route already differs from the legacy configuration; refusing to overwrite it.");
            }

            return new EmbeddingRouteImportResult(false, true);
        }

        if (route.Dimensions is not null || route.EmbeddingProfileFingerprint is not null ||
            !string.Equals(route.IndexState, "unconfigured", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The database embedding route is incomplete; refusing to import over it.");
        }

        var now = timeProvider.GetUtcNow();
        var protectedApiKey = apiKeyProtector.Protect(apiKey);
        if (!string.Equals(apiKeyProtector.Unprotect(protectedApiKey), apiKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The imported inference API key could not be verified after protection.");
        }

        var provider = new InferenceProviderRecord
        {
            Id = idGenerator.NewId(),
            Name = providerName,
            ProtocolCode = "openai-compatible",
            BaseUrl = endpointValue,
            ProtectedApiKey = protectedApiKey,
            VerificationState = "unverified",
            CreatedAt = now,
            UpdatedAt = now
        };
        route.Provider = provider;
        route.ProviderId = provider.Id;
        route.ModelName = modelName;
        route.Dimensions = dimensions;
        route.TimeoutSeconds = timeoutSeconds;
        route.EmbeddingProfileFingerprint = fingerprint;
        route.IndexState = "ready";
        route.UpdatedAt = now;
        db.InferenceProviders.Add(provider);

        var legacyCollection = VectorCollectionNamePolicy.CreateContextCollectionName(
            providerName,
            modelName,
            dimensions);
        var importedCollection = VectorCollectionNamePolicy.CreateContextCollectionName(
            route.Provider.Name,
            route.ModelName,
            route.Dimensions.Value);
        if (!string.Equals(legacyCollection, importedCollection, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The imported embedding profile would select a different vector collection.");
        }

        return new EmbeddingRouteImportResult(true, true);
    }

    private static string? ReadLegacyValue(IConfiguration configuration, string targetKey)
    {
        var currentValue = configuration[targetKey];
        if (currentValue is not null || !targetKey.StartsWith("ContextDepot:IndexRepair:", StringComparison.OrdinalIgnoreCase))
        {
            return currentValue;
        }

        var propertyName = targetKey["ContextDepot:IndexRepair:".Length..];
        return configuration[$"ContextDepot:Embedding:Repair:{propertyName}"];
    }

    private static string ConvertToJsonScalar(string key, string value)
    {
        if (key.EndsWith("Language", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith("Theme", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(value);
        }

        if (key.EndsWith("Threshold", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                !double.IsFinite(number))
            {
                throw new InvalidOperationException($"Legacy application setting '{key}' is not a valid number.");
            }

            return JsonSerializer.Serialize(number);
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            throw new InvalidOperationException($"Legacy application setting '{key}' is not a valid integer.");
        }

        return JsonSerializer.Serialize(integer);
    }

    private sealed record EmbeddingRouteImportResult(bool Imported, bool Configured);
}

public sealed record LegacyConfigurationImportResult(
    int ImportedApplicationSettingCount,
    bool EmbeddingRouteImported,
    bool EmbeddingRouteConfigured);
