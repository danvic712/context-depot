using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Npgsql;
using System.Runtime.CompilerServices;
using VectorStoreBase = Microsoft.Extensions.VectorData.VectorStore;

namespace ContextDepot.Infrastructure.VectorStore;

public sealed class PostgreSqlVectorStore(
    NpgsqlDataSource dataSource,
    IOptions<PostgreSqlVectorStoreOptions> options) : VectorStoreBase
{
    private readonly NpgsqlDataSource _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    private readonly PostgreSqlVectorStoreOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly VectorStoreMetadata _metadata = new()
    {
        VectorStoreSystemName = "PostgreSQL",
        VectorStoreName = "pgvector"
    };

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition)
        where TRecord : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var collectionDefinition = definition ?? throw new ArgumentNullException(nameof(definition));
        return new PostgreSqlVectorStoreCollection<TKey, TRecord>(_dataSource, _options.Schema, name, collectionDefinition);
    }

    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition)
    {
        throw new NotSupportedException("Dynamic vector records are not supported by the PostgreSQL provider.");
    }

    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT relation.relname
            FROM pg_catalog.pg_class AS relation
            INNER JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = @schema
              AND relation.relkind IN ('r', 'p')
              AND (relation.relname LIKE 'context_vectors_%' OR relation.relname LIKE 'document_vectors_%')
            ORDER BY relation.relname;
            """;
        command.Parameters.AddWithValue("schema", _options.Schema);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return reader.GetString(0);
        }
    }

    public override async Task<bool> CollectionExistsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
        command.Parameters.AddWithValue("schema", _options.Schema);
        command.Parameters.AddWithValue("name", name);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public override async Task EnsureCollectionDeletedAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {PostgreSqlVectorSqlBuilder.QuoteQualifiedName(_options.Schema, name)};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(VectorStoreMetadata))
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
}
