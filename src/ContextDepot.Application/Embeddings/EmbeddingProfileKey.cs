namespace ContextDepot.Application.Embeddings;

public sealed record EmbeddingProfileKey
{
    public EmbeddingProfileKey(string provider, string model, int dimensions)
    {
        Provider = string.IsNullOrWhiteSpace(provider)
            ? throw new ArgumentException("The embedding provider is required.", nameof(provider))
            : provider.Trim();
        Model = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("The embedding model is required.", nameof(model))
            : model.Trim();
        Dimensions = dimensions > 0
            ? dimensions
            : throw new ArgumentOutOfRangeException(nameof(dimensions));
    }

    public string Provider { get; }

    public string Model { get; }

    public int Dimensions { get; }

    public override string ToString() => $"{Provider}\n{Model}\n{Dimensions}";
}
