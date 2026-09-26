using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Depots.Contracts;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Infrastructure.Contracts;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.HealthChecks;
using ContextDepot.Infrastructure.Jobs;
using ContextDepot.Infrastructure.Markdown;
using ContextDepot.Infrastructure.Options;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;

namespace ContextDepot.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ContextDepot");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DatabaseUnavailable);
        }

        if (configuration is not IConfigurationBuilder configurationBuilder)
        {
            throw new InvalidOperationException("Database application settings require a mutable configuration builder.");
        }

        var settingsConfigurationSource = new DatabaseApplicationSettingsConfigurationSource();
        configurationBuilder.Add(settingsConfigurationSource);
        var settingsConfigurationProvider = settingsConfigurationSource.Provider
            ?? throw new InvalidOperationException("The database application settings provider was not initialized.");
        services.AddSingleton(settingsConfigurationProvider);
        services.AddSingleton<DatabaseApplicationSettingsSnapshotBuilder>();
        services.AddSingleton<DatabaseApplicationSettingsSnapshotLoader>();
        services.AddSingleton<InferenceRuntimeSnapshotAccessor>();
        services.AddSingleton<InferenceRuntimeSnapshotLoader>();
        services.AddSingleton<IConfigureOptions<EmbeddingOptions>, ConfigureEmbeddingOptionsFromInferenceSnapshot>();
        services.AddSingleton<ContextDepotStartupInitializer>();
        services.AddSingleton<IndexRepairCycleRunner>();

        services.AddDbContext<ContextDepotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public")));
        services.AddMemoryCache();
        services.AddOptions<VectorCoverageOptions>()
            .Bind(configuration.GetSection("ContextDepot:VectorCoverage"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<VectorCoverageOptions>, VectorCoverageOptionsValidator>();
        services.AddOptions<PostgreSqlVectorStoreOptions>()
            .Bind(configuration.GetSection("ContextDepot:VectorStore"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Schema), "The vector store schema is required.")
            .ValidateOnStart();
        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });
        services.AddSingleton<PostgreSqlVectorStore>();
        services.AddSingleton<VectorCollectionInitializer>();
        services.AddScoped<VectorCoverageSnapshotProvider>();
        services.AddScoped<IDepotRepository, DepotRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IContextRepository, ContextRepository>();
        services.AddScoped<IContextQueryRepository, ContextQueryRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IIndexRepairRepository, PostgreSqlIndexRepairRepository>();
        services.AddScoped<ISemanticRetrievalRepository, VectorDataSemanticRetrievalRepository>();
        services.AddScoped<IBootstrapRepository, BootstrapRepository>();
        services.AddScoped<IVectorIndexRepository, VectorDataVectorIndexRepository>();
        services.AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>("postgresql")
            .AddCheck<VectorCoverageHealthCheck>("vector_coverage");

        services.AddScoped<CurrentDepotAccessContext>();
        services.AddScoped<ICurrentDepotContext>(sp => sp.GetRequiredService<CurrentDepotAccessContext>());
        services.AddScoped<IWorkspaceAccessContext>(sp => sp.GetRequiredService<CurrentDepotAccessContext>());
        services.AddSingleton<DepotAccessKeySecretHasher>();
        services.AddScoped<IDepotAccessKeyAuthenticator, DepotAccessKeyAuthenticator>();
        services.AddSingleton<IIdGenerator, GuidV7IdGenerator>();

        services.AddOptions<MarkdownStoreOptions>()
            .Bind(configuration.GetSection("ContextDepot"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Root), ApplicationErrorMessages.Get(ApplicationErrorCodes.MarkdownRootUnavailable))
            .ValidateOnStart();
        services.AddSingleton<FileSystemMarkdownStore>();
        services.AddSingleton<IMarkdownStore>(sp => sp.GetRequiredService<FileSystemMarkdownStore>());
        services.AddHealthChecks().AddCheck<MarkdownStoreHealthCheck>("markdown_root");

        return services;
    }
}
