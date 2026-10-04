using ContextDepot.Infrastructure.Exceptions;
using Microsoft.Extensions.VectorData;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ContextDepot.Infrastructure.VectorStore;

internal static class PostgreSqlVectorSqlBuilder
{
    public static string BuildCreateTable(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition)
    {
        var properties = GetProperties(definition);
        var key = GetKeyProperty(properties);
        var columns = properties
            .Select(property => $"{QuoteIdentifier(GetStorageName(property))} {GetColumnType(property)} NOT NULL")
            .ToArray();

        var table = QuoteQualifiedName(schema, collectionName);
        var builder = new StringBuilder()
            .Append("CREATE SCHEMA IF NOT EXISTS ")
            .Append(QuoteIdentifier(schema))
            .Append(';')
            .Append("CREATE TABLE IF NOT EXISTS ")
            .Append(table)
            .Append(" (")
            .Append(string.Join(", ", columns))
            .Append(", CONSTRAINT ")
            .Append(QuoteIdentifier($"pk_{collectionName}"))
            .Append(" PRIMARY KEY (")
            .Append(QuoteIdentifier(GetStorageName(key)))
            .Append(") );");

        foreach (var dataProperty in properties.OfType<VectorStoreDataProperty>().Where(property => property.IsIndexed))
        {
            var storageName = GetStorageName(dataProperty);
            builder
                .Append("CREATE INDEX IF NOT EXISTS ")
                .Append(QuoteIdentifier($"ix_{collectionName}_{storageName}"))
                .Append(" ON ")
                .Append(table)
                .Append(" (")
                .Append(QuoteIdentifier(storageName))
                .Append(");");
        }

        return builder.ToString();
    }

    public static string BuildGet(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition,
        bool includeVectors)
    {
        var key = GetKeyProperty(definition.Properties.ToArray());
        return $"{BuildSelect(schema, collectionName, definition, includeVectors)} WHERE {QuoteIdentifier(GetStorageName(key))} = @key;";
    }

    public static string BuildGetMany(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition,
        bool includeVectors,
        IReadOnlyList<string> parameterNames)
    {
        if (parameterNames.Count == 0)
        {
            return BuildSelect(schema, collectionName, definition, includeVectors) + " WHERE FALSE;";
        }

        var key = GetKeyProperty(definition.Properties.ToArray());
        var parameters = string.Join(", ", parameterNames.Select(parameter => $"@{parameter}"));
        return $"{BuildSelect(schema, collectionName, definition, includeVectors)} WHERE {QuoteIdentifier(GetStorageName(key))} IN ({parameters});";
    }

    public static string BuildFilteredGet(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition,
        bool includeVectors,
        string filterSql,
        int top,
        int skip)
    {
        return $"{BuildSelect(schema, collectionName, definition, includeVectors)} WHERE {filterSql} LIMIT @top OFFSET @skip;";
    }

    public static string BuildDelete(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition)
    {
        var key = GetKeyProperty(definition.Properties.ToArray());
        return $"DELETE FROM {QuoteQualifiedName(schema, collectionName)} WHERE {QuoteIdentifier(GetStorageName(key))} = @key;";
    }

    public static string BuildDeleteMany(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition,
        IReadOnlyList<string> parameterNames)
    {
        if (parameterNames.Count == 0)
        {
            return "SELECT 1 WHERE FALSE;";
        }

        var key = GetKeyProperty(definition.Properties.ToArray());
        var parameters = string.Join(", ", parameterNames.Select(parameter => $"@{parameter}"));
        return $"DELETE FROM {QuoteQualifiedName(schema, collectionName)} WHERE {QuoteIdentifier(GetStorageName(key))} IN ({parameters});";
    }

    public static string BuildUpsert(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition)
    {
        var properties = GetProperties(definition);
        var table = QuoteQualifiedName(schema, collectionName);
        var columns = string.Join(", ", properties.Select(property => QuoteIdentifier(GetStorageName(property))));
        var values = string.Join(", ", properties.Select(property => $"@{GetParameterName(property)}"));
        var key = GetKeyProperty(properties);
        var keyName = GetStorageName(key);
        var updates = string.Join(", ", properties
            .Where(property => property is not VectorStoreKeyProperty)
            .Select(property => $"{QuoteIdentifier(GetStorageName(property))} = EXCLUDED.{QuoteIdentifier(GetStorageName(property))}"));

        return $"INSERT INTO {table} ({columns}) VALUES ({values}) ON CONFLICT ({QuoteIdentifier(keyName)}) DO UPDATE SET {updates};";
    }

    public static string BuildSearch(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition,
        bool includeVectors,
        string filterSql)
    {
        var vector = GetVectorProperty(definition.Properties.ToArray());
        var vectorName = QuoteIdentifier(GetStorageName(vector));
        var selected = BuildSelect(schema, collectionName, definition, includeVectors);
        var score = $"GREATEST(0.0::double precision, 1.0::double precision - ({vectorName} <=> @query_vector)) AS \"__vector_score\"";
        var prefix = selected.Replace(" FROM ", $", {score} FROM ", StringComparison.Ordinal);

        return $"{prefix} WHERE {filterSql} ORDER BY {vectorName} <=> @query_vector LIMIT @top OFFSET @skip;";
    }

    public static string QuoteQualifiedName(string schema, string collectionName) =>
        $"{QuoteIdentifier(schema)}.{QuoteIdentifier(collectionName)}";

    internal static string BuildSourceExists(string vectorQualifier, bool context)
    {
        if (context)
        {
            return $"""
                EXISTS (SELECT 1 FROM public.context_items AS source
                WHERE source.id = {vectorQualifier}.context_item_id
                  AND source.depot_id = {vectorQualifier}.depot_id
                  AND source.workspace_id = {vectorQualifier}.workspace_id
                  AND source.status = 'Active'
                  AND (source.valid_from IS NULL OR source.valid_from <= @source_now)
                  AND (source.valid_until IS NULL OR source.valid_until > @source_now)
                  AND (source.expires_at IS NULL OR source.expires_at > @source_now))
                """;
        }

        return $"""
            EXISTS (SELECT 1 FROM public.document_chunks AS source
            INNER JOIN public.documents AS document ON document.id = source.document_id
            WHERE source.id = {vectorQualifier}.document_chunk_id
              AND source.document_id = {vectorQualifier}.document_id
              AND source.depot_id = {vectorQualifier}.depot_id
              AND source.workspace_id = {vectorQualifier}.workspace_id
              AND document.status = 'Active' AND document.index_status = 'Indexed')
            """;
    }

    public static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Contains('\0'))
        {
            throw new ArgumentException(InfrastructureErrorCodes.PostgreSqlIdentifierInvalid, nameof(identifier));
        }

        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public static VectorStoreKeyProperty GetKeyProperty(IReadOnlyList<VectorStoreProperty> properties) =>
        properties.OfType<VectorStoreKeyProperty>().Single();

    public static VectorStoreVectorProperty GetVectorProperty(IReadOnlyList<VectorStoreProperty> properties) =>
        properties.OfType<VectorStoreVectorProperty>().Single();

    public static string GetStorageName(VectorStoreProperty property) =>
        string.IsNullOrWhiteSpace(property.StorageName)
            ? JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name)
            : property.StorageName;

    public static string GetParameterName(VectorStoreProperty property) =>
        $"record_{property.Name}";

    private static IReadOnlyList<VectorStoreProperty> GetProperties(VectorStoreCollectionDefinition definition)
    {
        if (definition.Properties is null || definition.Properties.Count == 0)
        {
            throw new InvalidOperationException(InfrastructureErrorCodes.VectorCollectionPropertiesRequired);
        }

        var properties = definition.Properties.ToArray();
        _ = GetKeyProperty(properties);
        _ = GetVectorProperty(properties);
        return properties;
    }

    private static string BuildSelect(
        string schema,
        string collectionName,
        VectorStoreCollectionDefinition definition,
        bool includeVectors)
    {
        var properties = GetProperties(definition)
            .Where(property => includeVectors || property is not VectorStoreVectorProperty)
            .Select(property => QuoteIdentifier(GetStorageName(property)));

        return $"SELECT {string.Join(", ", properties)} FROM {QuoteQualifiedName(schema, collectionName)}";
    }

    private static string GetColumnType(VectorStoreProperty property) => property switch
    {
        VectorStoreKeyProperty or VectorStoreDataProperty when property.Type == typeof(Guid) => "uuid",
        VectorStoreKeyProperty or VectorStoreDataProperty when property.Type == typeof(string) => "text",
        VectorStoreVectorProperty vector => $"vector({vector.Dimensions.ToString(CultureInfo.InvariantCulture)})",
        _ => throw new NotSupportedException(InfrastructureErrorCodes.VectorPropertyTypeUnsupported) { Data = { ["type"] = property.Type } }
    };
}
