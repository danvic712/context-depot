using ContextDepot.Application.Contexts;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ContextDepot.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DocumentWriteCoordinator>();
        services.AddSingleton<HeadingAwareMarkdownChunker>();
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
