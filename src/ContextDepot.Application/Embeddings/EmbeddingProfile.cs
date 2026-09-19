namespace ContextDepot.Application.Embeddings;

public sealed record EmbeddingProfile(EmbeddingProfileKey Key)
{
    public string Provider => Key.Provider;

    public string Model => Key.Model;

    public int Dimensions => Key.Dimensions;

    public static EmbeddingProfile From(EmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new EmbeddingProfile(new EmbeddingProfileKey(options.Provider, options.Model, options.Dimensions));
    }
}
