using ContextDepot.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Safety;
using ContextDepot.Infrastructure.CurrentOwner;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is required.");
        }

        services.AddDbContext<ContextDepotDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public")));
        services.AddScoped<IWorkspaceRepository, PostgreSqlWorkspaceRepository>();
        services.AddScoped<IContextRepository, PostgreSqlContextRepository>();
        services.AddScoped<IDocumentRepository, PostgreSqlDocumentRepository>();
        services.AddScoped<IBootstrapRepository, PostgreSqlBootstrapRepository>();
        services.AddScoped<CurrentOwnerBootstrapper>();
        return services;
    }

    public static IServiceCollection AddContextDepotApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CurrentOwnerOptions>()
            .Bind(configuration.GetSection("ContextDepot:Owner"))
            .Validate(options => options.Id != Guid.Empty, "ContextDepot:Owner:Id must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DisplayName) && options.DisplayName.Length <= 200, "ContextDepot:Owner:DisplayName must be between 1 and 200 characters.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CurrentOwnerOptions>>().Value);
        services.AddSingleton<ICurrentOwnerContext, ConfiguredCurrentOwnerContext>();
        services.AddSingleton<IIdGenerator, GuidV7IdGenerator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IWorkspaceAppService, WorkspaceAppService>();
        services.AddSingleton<ISecretDetector, HighConfidenceSecretDetector>();
        services.AddSingleton<IProvenancePolicy, ProvenancePolicy>();
        services.AddScoped<ISourceSafetyService, SourceSafetyService>();
        return services;
    }
}
