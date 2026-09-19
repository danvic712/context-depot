using ContextDepot.Application.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContextDepot.Infrastructure;

public sealed class ContextDepotDbContextFactory : IDesignTimeDbContextFactory<ContextDepotDbContext>
{
    public ContextDepotDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTEXT_DEPOT_DESIGNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                ApplicationErrorMessages.Get(ApplicationErrorCodes.DesignTimeConfigurationMissing));
        }

        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public"))
            .Options;

        return new ContextDepotDbContext(options);
    }
}
