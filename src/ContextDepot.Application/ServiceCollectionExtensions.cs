using ContextDepot.Application.Contexts;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.Settings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.SemanticRetrieval;
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
        services.AddOptions<EmbeddingOptions>();
        services.AddSingleton<IValidateOptions<EmbeddingOptions>, EmbeddingOptionsValidator>();
        services.AddOptions<RetrievalOptions>()
            .Bind(configuration.GetSection("ContextDepot:Retrieval"), binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RetrievalOptions>, RetrievalOptionsValidator>();
        services.AddOptions<IndexRepairOptions>()
            .Bind(configuration.GetSection("ContextDepot:IndexRepair"), binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<IndexRepairOptions>, IndexRepairOptionsValidator>();
        services.AddOptions<AppearanceOptions>()
            .Bind(configuration.GetSection("ContextDepot:Appearance"), binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AppearanceOptions>, AppearanceOptionsValidator>();
        services.AddSingleton<EmbeddingResultValidator>();
        services.AddSingleton<ContextEmbeddingTextBuilder>();
        services.AddSingleton<DocumentEmbeddingTextBuilder>();
        services.AddScoped<EmbeddingGeneratorService>();
        services.AddScoped<SemanticFallbackDecider>();
        services.AddSingleton<SemanticWorkspaceAggregator>();
        services.AddSingleton<HybridCandidateRanker>();
        services.AddScoped<RetrievalDeduplicator>();
        services.AddSingleton<ContextBudgetAllocator>();
        services.AddSingleton<ISecretDetector, HighConfidenceSecretDetector>();
        services.AddSingleton<IProvenancePolicy, ProvenancePolicy>();
        services.AddScoped<ISourceSafetyService, SourceSafetyService>();
        services.AddScoped<IWorkspaceAppService, WorkspaceAppService>();
        services.AddScoped<IContextAppService, ContextAppService>();
        services.AddScoped<IContextQueryAppService, ContextQueryAppService>();
        services.AddScoped<IDocumentAppService, DocumentAppService>();
        services.AddScoped<IContextBootstrapAppService, ContextBootstrapAppService>();
        services.AddScoped<VectorIndexRepairer>();
        services.AddScoped<IIndexRepairAppService, IndexRepairAppService>();
        return services;
    }
}
