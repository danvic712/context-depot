namespace ContextDepot.Application.Embeddings;

public sealed class EmbeddingOptions
{
    public string Provider { get; set; } = "ConfiguredGenerator";

    public string Model { get; set; } = "embedding-model";

    public int Dimensions { get; set; } = 1536;
}
