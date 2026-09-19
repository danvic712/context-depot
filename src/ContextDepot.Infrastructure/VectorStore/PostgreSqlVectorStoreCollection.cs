using Microsoft.Extensions.VectorData;
using Npgsql;
using Pgvector;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ContextDepot.Infrastructure.VectorStore;

public sealed class PostgreSqlVectorStoreCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schema;
    private readonly string _name;
    private readonly VectorStoreCollectionDefinition _definition;
    private readonly IReadOnlyList<VectorStoreProperty> _properties;
    private readonly IReadOnlyList<VectorStoreProperty> _selectedProperties;
    private readonly VectorStoreKeyProperty _keyProperty;
    private readonly VectorStoreVectorProperty _vectorProperty;
    private readonly IReadOnlyDictionary<string, PropertyInfo> _recordProperties;
    private readonly VectorStoreCollectionMetadata _metadata;

    public PostgreSqlVectorStoreCollection(
        NpgsqlDataSource dataSource,
        string schema,
        string name,
        VectorStoreCollectionDefinition definition)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _properties = definition.Properties?.ToArray()
            ?? throw new ArgumentException("The collection definition must declare properties.", nameof(definition));
        _keyProperty = PostgreSqlVectorSqlBuilder.GetKeyProperty(_properties);
        _vectorProperty = PostgreSqlVectorSqlBuilder.GetVectorProperty(_properties);
        _selectedProperties = _properties;
        _recordProperties = _properties.ToDictionary(
            property => property.Name,
            property => typeof(TRecord).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The record type '{typeof(TRecord).Name}' does not contain property '{property.Name}'."),
            StringComparer.Ordinal);

        _ = PostgreSqlVectorSqlBuilder.QuoteQualifiedName(_schema, _name);
        _metadata = new VectorStoreCollectionMetadata
        {
            VectorStoreSystemName = "PostgreSQL",
            VectorStoreName = "pgvector",
            CollectionName = _name
        };
    }

    public override string Name => _name;

    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_catalog.pg_class AS relation
                INNER JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
                WHERE namespace.nspname = @schema
                  AND relation.relname = @name
                  AND relation.relkind IN ('r', 'p')
            );
            """;
        command.Parameters.AddWithValue("schema", _schema);
        command.Parameters.AddWithValue("name", _name);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildCreateTable(_schema, _name, _definition);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {PostgreSqlVectorSqlBuilder.QuoteQualifiedName(_schema, _name)};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<TRecord?> GetAsync(
        TKey key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var includeVectors = options?.IncludeVectors == true;
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildGet(_schema, _name, _definition, includeVectors);
        command.Parameters.AddWithValue("key", ConvertKey(key));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRecord(reader, includeVectors)
            : default;
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(
        IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var keyValues = keys.Select(ConvertKey).ToArray();
        if (keyValues.Length == 0)
        {
            yield break;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var includeVectors = options?.IncludeVectors == true;
        var parameterNames = keyValues.Select((_, index) => $"key_{index}").ToArray();
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildGetMany(_schema, _name, _definition, includeVectors, parameterNames);
        for (var index = 0; index < keyValues.Length; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], keyValues[index]);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return ReadRecord(reader, includeVectors);
        }
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
        ArgumentOutOfRangeException.ThrowIfNegative(options?.Skip ?? 0);
        if (options?.OrderBy is not null)
        {
            throw new NotSupportedException("Ordered filtered record retrieval is not supported by the PostgreSQL provider.");
        }

        var translation = PostgreSqlVectorFilterTranslator.Translate(filter, _properties);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var includeVectors = options?.IncludeVectors == true;
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildFilteredGet(
            _schema,
            _name,
            _definition,
            includeVectors,
            translation.Sql,
            top,
            options?.Skip ?? 0);
        AddFilterParameters(command, translation);
        command.Parameters.AddWithValue("top", top);
        command.Parameters.AddWithValue("skip", options?.Skip ?? 0);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return ReadRecord(reader, includeVectors);
        }
    }

    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildDelete(_schema, _name, _definition);
        command.Parameters.AddWithValue("key", ConvertKey(key));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var keyValues = keys.Select(ConvertKey).ToArray();
        if (keyValues.Length == 0)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = keyValues.Select((_, index) => $"key_{index}").ToArray();
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildDeleteMany(_schema, _name, _definition, parameterNames);
        for (var index = 0; index < keyValues.Length; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], keyValues[index]);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await EnsureCollectionExistsAsync(cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildUpsert(_schema, _name, _definition);
        AddRecordParameters(command, record);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        var materializedRecords = records.ToArray();
        if (materializedRecords.Length == 0)
        {
            return;
        }

        await EnsureCollectionExistsAsync(cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var record in materializedRecords)
            {
                ArgumentNullException.ThrowIfNull(record);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = PostgreSqlVectorSqlBuilder.BuildUpsert(_schema, _name, _definition);
                AddRecordParameters(command, record);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput vector,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(top);
        var queryVector = ResolveQueryVector(vector);
        if (queryVector.Length != _vectorProperty.Dimensions)
        {
            throw new ArgumentException(
                $"The query vector has {queryVector.Length} dimensions, but the collection requires {_vectorProperty.Dimensions}.",
                nameof(vector));
        }

        var translation = PostgreSqlVectorFilterTranslator.Translate(options?.Filter, _properties);
        var filterSql = translation.Sql;
        if (options?.ScoreThreshold is { } threshold)
        {
            filterSql = $"({filterSql}) AND (GREATEST(0.0::double precision, 1.0::double precision - ({PostgreSqlVectorSqlBuilder.QuoteIdentifier(PostgreSqlVectorSqlBuilder.GetStorageName(_vectorProperty))} <=> @query_vector)) >= @score_threshold)";
        }

        var skip = options?.Skip ?? 0;
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var includeVectors = options?.IncludeVectors == true;
        command.CommandText = PostgreSqlVectorSqlBuilder.BuildSearch(_schema, _name, _definition, includeVectors, filterSql);
        command.Parameters.AddWithValue("query_vector", new Vector(queryVector));
        AddFilterParameters(command, translation);
        if (options?.ScoreThreshold is { } scoreThreshold)
        {
            command.Parameters.AddWithValue("score_threshold", scoreThreshold);
        }

        command.Parameters.AddWithValue("top", top);
        command.Parameters.AddWithValue("skip", skip);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var selectedCount = _selectedProperties.Count(property => includeVectors || property is not VectorStoreVectorProperty);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = ReadRecord(reader, includeVectors);
            var score = reader.IsDBNull(selectedCount) ? (double?)null : reader.GetDouble(selectedCount);
            yield return new VectorSearchResult<TRecord>(record, score);
        }
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(VectorStoreCollectionMetadata))
        {
            return _metadata;
        }

        if (serviceType.IsInstanceOfType(this))
        {
            return this;
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
    }

    private TRecord ReadRecord(NpgsqlDataReader reader, bool includeVectors)
    {
        var record = Activator.CreateInstance<TRecord>();
        var selectedProperties = _properties.Where(property => includeVectors || property is not VectorStoreVectorProperty).ToArray();
        for (var index = 0; index < selectedProperties.Length; index++)
        {
            var property = selectedProperties[index];
            var target = _recordProperties[property.Name];
            object value;
            if (property is VectorStoreVectorProperty)
            {
                value = reader.GetFieldValue<Vector>(index).Memory;
            }
            else if (property.Type == typeof(Guid))
            {
                value = reader.GetGuid(index);
            }
            else if (property.Type == typeof(string))
            {
                value = reader.GetString(index);
            }
            else
            {
                throw new NotSupportedException($"The record property '{property.Name}' is not supported by the PostgreSQL provider.");
            }

            target.SetValue(record, value);
        }

        return record;
    }

    private void AddRecordParameters(NpgsqlCommand command, TRecord record)
    {
        foreach (var property in _properties)
        {
            var value = _recordProperties[property.Name].GetValue(record);
            if (property is VectorStoreVectorProperty vectorProperty)
            {
                if (value is not ReadOnlyMemory<float> memory)
                {
                    throw new NotSupportedException($"The vector property '{property.Name}' must be ReadOnlyMemory<float>.");
                }

                if (memory.Length != vectorProperty.Dimensions)
                {
                    throw new ArgumentException(
                        $"The vector property '{property.Name}' has {memory.Length} dimensions, but the collection requires {vectorProperty.Dimensions}.",
                        property.Name);
                }

                command.Parameters.AddWithValue(PostgreSqlVectorSqlBuilder.GetParameterName(property), new Vector(memory));
            }
            else
            {
                command.Parameters.AddWithValue(PostgreSqlVectorSqlBuilder.GetParameterName(property), value ?? DBNull.Value);
            }
        }
    }

    private static void AddFilterParameters(NpgsqlCommand command, PostgreSqlVectorFilterTranslation translation)
    {
        foreach (var (name, value) in translation.Parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    private static Guid ConvertKey(TKey key)
    {
        if (key is Guid value)
        {
            return value;
        }

        throw new NotSupportedException($"The key type '{typeof(TKey).Name}' is not supported by the PostgreSQL provider.");
    }

    private static ReadOnlyMemory<float> ResolveQueryVector<TInput>(TInput input)
    {
        object? value = input;
        if (value is ReadOnlyMemory<float> memory)
        {
            return memory;
        }

        if (value is float[] array)
        {
            return array;
        }

        if (value is Vector vector)
        {
            return vector.Memory;
        }

        var vectorProperty = value?.GetType().GetProperty("Vector", BindingFlags.Public | BindingFlags.Instance);
        if (vectorProperty?.PropertyType == typeof(ReadOnlyMemory<float>) && vectorProperty.GetValue(value) is ReadOnlyMemory<float> embedding)
        {
            return embedding;
        }

        throw new NotSupportedException($"The query vector type '{typeof(TInput).Name}' is not supported by the PostgreSQL provider.");
    }
}
