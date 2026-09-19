using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Owners;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure;

public sealed class ContextDepotDbContext(DbContextOptions<ContextDepotDbContext> options)
    : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<ContextItem> ContextItems => Set<ContextItem>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContextDepotDbContext).Assembly);
    }
}
