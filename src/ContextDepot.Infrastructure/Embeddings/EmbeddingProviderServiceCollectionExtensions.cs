using System.ClientModel;
using ContextDepot.Application.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace ContextDepot.Infrastructure.Embeddings;

public static class EmbeddingProviderServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotEmbeddingProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("ContextDepot:Embedding");
        var adapter = section["Adapter"];
        if (string.IsNullOrWhiteSpace(adapter) ||
            string.Equals(adapter, "None", StringComparison.OrdinalIgnoreCase))
        {
            // A deployment may supply another IEmbeddingGenerator, or run with lexical-only retrieval.
            return services;
        }

        if (!string.Equals(adapter, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ContextDepot:Embedding:Adapter is not supported.");
        }

        var provider = section[nameof(EmbeddingOptions.Provider)];
        var model = section[nameof(EmbeddingOptions.Model)];
        var key = section["OpenAICompatible:ApiKey"];
        var endpoint = section["OpenAICompatible:Endpoint"];
        var timeoutText = section["OpenAICompatible:TimeoutSeconds"];
        if (string.IsNullOrWhiteSpace(provider) ||
            string.Equals(provider, "ConfiguredGenerator", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(model) ||
            string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "ContextDepot:Embedding:Provider, Model and OpenAICompatible:ApiKey are required when Adapter is OpenAICompatible.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "ContextDepot:Embedding:OpenAICompatible:Endpoint must be an absolute HTTP or HTTPS URL.");
        }

        if (!int.TryParse(timeoutText, out var timeoutSeconds) || timeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                "ContextDepot:Embedding:OpenAICompatible:TimeoutSeconds must be between 1 and 300.");
        }

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
        {
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = endpointUri,
                NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            var client = new OpenAIClient(new ApiKeyCredential(key), clientOptions);
            return client.GetEmbeddingClient(model).AsIEmbeddingGenerator();
        });

        return services;
    }
}
