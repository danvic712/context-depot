using ContextDepot.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        return services;
    }
}
