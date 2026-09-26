using ContextDepot.Application.Embeddings;
using ContextDepot.Infrastructure.Embeddings;
using ContextDepot.Infrastructure.Options;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Tests;

public sealed class EmbeddingRuntimeTests
{
    [Fact]
    public void New_scopes_use_the_latest_embedding_profile_and_generator()
    {
        var services = new ServiceCollection();
        services.AddOptions<EmbeddingOptions>();
        services.AddSingleton<InferenceRuntimeSnapshotAccessor>();
        services.AddScoped(sp => new ScopedInferenceRuntimeSnapshot(
            sp.GetRequiredService<InferenceRuntimeSnapshotAccessor>().Current));
        services.AddScoped<IConfigureOptions<EmbeddingOptions>, ConfigureEmbeddingOptionsFromInferenceSnapshot>();
        services.AddContextDepotEmbeddingProvider();
        using var provider = services.BuildServiceProvider();
        var snapshots = provider.GetRequiredService<InferenceRuntimeSnapshotAccessor>();

        snapshots.Publish(new InferenceRuntimeSnapshot(null, InferenceRuntimeState.Unconfigured, null));
        using (var scope = provider.CreateScope())
        {
            Assert.Null(scope.ServiceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>());
        }

        snapshots.Publish(ReadySnapshot("https://first.example.test", "first-model", 3));
        IEmbeddingGenerator<string, Embedding<float>> firstGenerator;
        using (var scope = provider.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<EmbeddingOptions>>().Value;
            Assert.Equal("first-model", options.Model);
            Assert.Equal(3, options.Dimensions);
            firstGenerator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

            snapshots.Publish(ReadySnapshot("https://second.example.test", "second-model", 4));
            Assert.Equal("first-model", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<EmbeddingOptions>>().Value.Model);
            Assert.Same(firstGenerator,
                scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
        }

        using (var scope = provider.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<EmbeddingOptions>>().Value;
            Assert.Equal("second-model", options.Model);
            Assert.Equal(4, options.Dimensions);
            var secondGenerator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            Assert.NotSame(firstGenerator, secondGenerator);
        }
    }

    [Fact]
    public void Different_provider_endpoints_use_different_vector_collections()
    {
        var first = ReadySnapshot("https://first.example.test", "model", 3).Embedding!;
        var second = ReadySnapshot("https://second.example.test", "model", 3).Embedding!;

        Assert.NotEqual(
            VectorCollectionNamePolicy.CreateContextCollectionName(first.ProfileFingerprint),
            VectorCollectionNamePolicy.CreateContextCollectionName(second.ProfileFingerprint));
        Assert.NotEqual(
            VectorCollectionNamePolicy.CreateDocumentCollectionName(first.ProfileFingerprint),
            VectorCollectionNamePolicy.CreateDocumentCollectionName(second.ProfileFingerprint));
    }

    private static InferenceRuntimeSnapshot ReadySnapshot(string endpoint, string model, int dimensions)
    {
        var fingerprint = EmbeddingProfileFingerprint.Compute("provider", "openai-compatible", endpoint, model, dimensions);
        return new InferenceRuntimeSnapshot(
            new EmbeddingRouteRuntimeSnapshot(
                "provider", "openai-compatible", new Uri(endpoint), "test-key", model, dimensions, 30, fingerprint),
            InferenceRuntimeState.Ready,
            null);
    }
}
