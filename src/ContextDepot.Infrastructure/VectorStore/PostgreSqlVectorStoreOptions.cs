namespace ContextDepot.Infrastructure.VectorStore;

public sealed class PostgreSqlVectorStoreOptions
{
    public string Schema { get; init; } = "public";
}
