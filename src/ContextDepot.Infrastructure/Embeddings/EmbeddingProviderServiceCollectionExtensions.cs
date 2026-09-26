using System.ClientModel;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;

namespace ContextDepot.Infrastructure.Embeddings;

public static class EmbeddingProviderServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotEmbeddingProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
        {
            var embedding = serviceProvider
                .GetRequiredService<ScopedInferenceRuntimeSnapshot>()
                .Value
                .Embedding;
            if (embedding is null)
            {
                return null!;
            }

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = embedding.Endpoint,
                NetworkTimeout = TimeSpan.FromSeconds(embedding.TimeoutSeconds)
            };
            var client = new OpenAIClient(new ApiKeyCredential(embedding.ApiKey), clientOptions);
            return client.GetEmbeddingClient(embedding.ModelName).AsIEmbeddingGenerator();
        });

        return services;
    }
}
