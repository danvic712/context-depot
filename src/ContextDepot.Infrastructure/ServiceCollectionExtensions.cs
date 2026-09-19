using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Infrastructure.CurrentOwner;
using ContextDepot.Infrastructure.HealthChecks;
using ContextDepot.Infrastructure.Markdown;
using ContextDepot.Infrastructure.Options;
using ContextDepot.Infrastructure.Persistence;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.Shared;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
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

        services.AddDbContext<ContextDepotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public")));
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
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IContextRepository, ContextRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IBootstrapRepository, BootstrapRepository>();
        services.AddScoped<CurrentOwnerBootstrapper>();
        services.AddHostedService<ContextDepotInfrastructureInitializer>();
        services.AddHealthChecks()
            .AddCheck<PostgreSqlHealthCheck>("postgresql")
            .AddCheck<CurrentOwnerHealthCheck>("current_owner");

        services.AddOptions<CurrentOwnerOptions>()
            .Bind(configuration.GetSection("ContextDepot:Owner"))
            .Validate(options => options.Id != Guid.Empty, ApplicationErrorMessages.Get(ApplicationErrorCodes.OwnerNotConfigured))
            .Validate(options => options.Id.Version == 7, ApplicationErrorMessages.Get(ApplicationErrorCodes.OwnerNotConfigured))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DisplayName) && options.DisplayName.Length <= 200, ApplicationErrorMessages.Get(ApplicationErrorCodes.OwnerNotConfigured))
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CurrentOwnerOptions>>().Value);
        services.AddSingleton<ICurrentOwnerContext, ConfiguredCurrentOwnerContext>();
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
