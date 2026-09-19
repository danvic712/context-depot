namespace ContextDepot.Infrastructure.VectorStore;

internal sealed class PostgreSqlVectorFilterTranslation(
    string sql,
    IReadOnlyList<(string Name, object? Value)> parameters)
{
    public string Sql { get; } = sql;

    public IReadOnlyList<(string Name, object? Value)> Parameters { get; } = parameters;
}
