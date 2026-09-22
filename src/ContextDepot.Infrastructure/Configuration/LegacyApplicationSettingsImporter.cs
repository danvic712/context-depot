using System.Globalization;
using System.Text.Json;
using ContextDepot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContextDepot.Infrastructure;

public sealed class LegacyApplicationSettingsImporter(
    ContextDepotDbContext db,
    DatabaseApplicationSettingsSnapshotBuilder snapshotBuilder,
    TimeProvider timeProvider)
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

    public async Task<int> ImportAsync(IConfiguration legacyConfiguration, CancellationToken cancellationToken)
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
        if (importedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return importedCount;
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
}
