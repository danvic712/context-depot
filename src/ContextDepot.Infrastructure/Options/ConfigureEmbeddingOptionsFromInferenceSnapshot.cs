using ContextDepot.Application.Embeddings;
using ContextDepot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Options;

public sealed class ConfigureEmbeddingOptionsFromInferenceSnapshot(
    InferenceRuntimeSnapshotAccessor snapshotAccessor) : IConfigureOptions<EmbeddingOptions>
{
    public void Configure(EmbeddingOptions options)
    {
        var embedding = snapshotAccessor.Current.Embedding;
        if (embedding is null)
        {
            return;
        }

        options.Provider = embedding.ProviderName;
        options.Model = embedding.ModelName;
        options.Dimensions = embedding.Dimensions;
    }
}
