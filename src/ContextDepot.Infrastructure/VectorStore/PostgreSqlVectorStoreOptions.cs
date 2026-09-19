namespace ContextDepot.Infrastructure.VectorStore;

public sealed class PostgreSqlVectorStoreOptions
{
    public string Schema { get; init; } = "public";

    public string Provider { get; init; } = "ConfiguredGenerator";

    public string Model { get; init; } = "embedding-model";

    public int Dimensions { get; init; } = 1536;
}
