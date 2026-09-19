using ContextDepot.Application.Contexts;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DocumentWriteCoordinator>();
        services.AddSingleton<HeadingAwareMarkdownChunker>();
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection("ContextDepot:Embedding"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmbeddingOptions>, EmbeddingOptionsValidator>();
        services.AddSingleton<IValidateOptions<EmbeddingOptions>, EmbeddingGeneratorRegistrationValidator>();
        services.AddOptions<RetrievalOptions>()
            .Bind(configuration.GetSection("ContextDepot:Retrieval"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RetrievalOptions>, RetrievalOptionsValidator>();
        services.AddSingleton<EmbeddingResultValidator>();
        services.AddSingleton<EmbeddingGeneratorService>();
        services.AddSingleton<ISecretDetector, HighConfidenceSecretDetector>();
        services.AddSingleton<IProvenancePolicy, ProvenancePolicy>();
        services.AddScoped<ISourceSafetyService, SourceSafetyService>();
        services.AddScoped<IWorkspaceAppService, WorkspaceAppService>();
        services.AddScoped<IContextAppService, ContextAppService>();
        services.AddScoped<IDocumentAppService, DocumentAppService>();
        services.AddScoped<IContextBootstrapAppService, ContextBootstrapAppService>();
        return services;
    }
}
