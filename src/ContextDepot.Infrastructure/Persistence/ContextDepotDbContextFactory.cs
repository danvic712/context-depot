using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContextDepot.Infrastructure.Persistence;

public sealed class ContextDepotDbContextFactory : IDesignTimeDbContextFactory<ContextDepotDbContext>
{
    public ContextDepotDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=context_depot;Username=context_depot;Password=context_depot_dev_only", npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public"))
            .Options;

        return new ContextDepotDbContext(options);
    }
}
