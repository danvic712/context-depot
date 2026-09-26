using ContextDepot.Application.Embeddings;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Options;

public sealed class ConfigureEmbeddingOptionsFromInferenceSnapshot(
    ScopedInferenceRuntimeSnapshot snapshot) : IConfigureOptions<EmbeddingOptions>
{
    public void Configure(EmbeddingOptions options)
    {
        var embedding = snapshot.Value.Embedding;
        if (embedding is null)
        {
            return;
        }

        options.Provider = embedding.ProviderName;
        options.Model = embedding.ModelName;
        options.Dimensions = embedding.Dimensions;
    }
}
