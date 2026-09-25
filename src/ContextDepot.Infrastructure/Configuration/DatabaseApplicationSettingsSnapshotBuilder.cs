using System.Text.Json;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.Settings;
using ContextDepot.Domain.Settings;
using ContextDepot.Infrastructure.Options;
using Microsoft.Extensions.Configuration;

namespace ContextDepot.Infrastructure.Configuration;

public sealed class DatabaseApplicationSettingsSnapshotBuilder
{
    private static readonly IReadOnlyDictionary<string, string> SupportedKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContextDepot:Retrieval:Semantic:ScopeTopK"] = "ContextDepot:Retrieval:Semantic:ScopeTopK",
            ["ContextDepot:Retrieval:Semantic:CandidateTopKPerSource"] = "ContextDepot:Retrieval:Semantic:CandidateTopKPerSource",
            ["ContextDepot:Retrieval:Semantic:OversampleFactor"] = "ContextDepot:Retrieval:Semantic:OversampleFactor",
            ["ContextDepot:Retrieval:Semantic:RetrievalLexicalFallbackThreshold"] = "ContextDepot:Retrieval:Semantic:RetrievalLexicalFallbackThreshold",
            ["ContextDepot:Retrieval:Semantic:DedupSimilarityThreshold"] = "ContextDepot:Retrieval:Semantic:DedupSimilarityThreshold",
            ["ContextDepot:Retrieval:Semantic:DedupTokenOverlapThreshold"] = "ContextDepot:Retrieval:Semantic:DedupTokenOverlapThreshold",
            ["ContextDepot:Retrieval:Search:DefaultLimit"] = "ContextDepot:Retrieval:Search:DefaultLimit",
            ["ContextDepot:Retrieval:Search:MaxLimit"] = "ContextDepot:Retrieval:Search:MaxLimit",
            ["ContextDepot:IndexRepair:PollIntervalSeconds"] = "ContextDepot:IndexRepair:PollIntervalSeconds",
            ["ContextDepot:IndexRepair:BatchSize"] = "ContextDepot:IndexRepair:BatchSize",
            ["ContextDepot:IndexRepair:MaxBatchesPerCycle"] = "ContextDepot:IndexRepair:MaxBatchesPerCycle",
            ["ContextDepot:VectorCoverage:CacheDurationSeconds"] = "ContextDepot:VectorCoverage:CacheDurationSeconds",
            ["ContextDepot:Appearance:Language"] = "ContextDepot:Appearance:Language",
            ["ContextDepot:Appearance:Theme"] = "ContextDepot:Appearance:Theme"
        };

    public IReadOnlyDictionary<string, string?> Build(IEnumerable<ApplicationSetting> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (!SupportedKeys.TryGetValue(record.Key, out var canonicalKey))
            {
                throw new InvalidOperationException($"Unsupported application setting key '{record.Key}'.");
            }

            if (!values.TryAdd(canonicalKey, ConvertJsonScalar(record.Key, record.ValueJson)))
            {
                throw new InvalidOperationException($"Application setting key '{record.Key}' is duplicated.");
            }
        }

        var missingKeys = SupportedKeys.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(key => !values.ContainsKey(key))
            .ToArray();
        if (missingKeys.Length > 0)
        {
            throw new InvalidOperationException(
                $"Required application settings are missing: {string.Join(", ", missingKeys)}.");
        }

        ValidateOptions(values);
        return values;
    }

    private static string ConvertJsonScalar(string key, string valueJson)
    {
        try
        {
            using var document = JsonDocument.Parse(valueJson);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString()!,
                JsonValueKind.Number => document.RootElement.GetRawText(),
                JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                _ => throw new InvalidOperationException($"Application setting '{key}' must be a JSON string, number or boolean.")
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Application setting '{key}' contains invalid JSON.", exception);
        }
    }

    private static void ValidateOptions(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var retrieval = Bind<RetrievalOptions>(configuration, "ContextDepot:Retrieval");
        if (!new RetrievalOptionsValidator().Validate(null, retrieval).Succeeded)
        {
            throw new InvalidOperationException("Database retrieval settings failed validation.");
        }

        var indexRepair = Bind<IndexRepairOptions>(configuration, "ContextDepot:IndexRepair");
        if (!new IndexRepairOptionsValidator().Validate(null, indexRepair).Succeeded)
        {
            throw new InvalidOperationException("Database index repair settings failed validation.");
        }

        var vectorCoverage = Bind<VectorCoverageOptions>(configuration, "ContextDepot:VectorCoverage");
        if (!new VectorCoverageOptionsValidator().Validate(null, vectorCoverage).Succeeded)
        {
            throw new InvalidOperationException("Database vector coverage settings failed validation.");
        }

        var appearance = Bind<AppearanceOptions>(configuration, "ContextDepot:Appearance");
        if (!new AppearanceOptionsValidator().Validate(null, appearance).Succeeded)
        {
            throw new InvalidOperationException("Database appearance settings failed validation.");
        }
    }

    private static T Bind<T>(IConfiguration configuration, string sectionName)
        where T : new()
    {
        var options = new T();
        configuration.GetSection(sectionName).Bind(
            options,
            binder => binder.ErrorOnUnknownConfiguration = true);
        return options;
    }
}
