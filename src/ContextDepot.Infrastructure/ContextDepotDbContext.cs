using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Inferences;
using ContextDepot.Domain.Settings;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure;

public sealed class ContextDepotDbContext(
    DbContextOptions<ContextDepotDbContext> options,
    IWorkspaceAccessContext workspaceAccessContext)
    : DbContext(options)
{
    public bool HasUnrestrictedWorkspaceAccess => workspaceAccessContext.HasUnrestrictedAccess;

    public IReadOnlyList<Guid> AccessibleWorkspaceIds => workspaceAccessContext.WorkspaceIds;

    public IReadOnlyList<Guid> NavigableWorkspaceIds => workspaceAccessContext.NavigableWorkspaceIds;

    public DbSet<Depot> Depots => Set<Depot>();

    public DbSet<DepotAccessKey> DepotAccessKeys => Set<DepotAccessKey>();

    public DbSet<WorkspaceAccessGrant> WorkspaceAccessGrants => Set<WorkspaceAccessGrant>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<ContextItem> ContextItems => Set<ContextItem>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    public DbSet<InferenceProvider> InferenceProviders => Set<InferenceProvider>();

    public DbSet<InferenceRoute> InferenceRoutes => Set<InferenceRoute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContextDepotDbContext).Assembly);
        modelBuilder.Entity<Workspace>()
            .HasQueryFilter(workspace =>
                HasUnrestrictedWorkspaceAccess || NavigableWorkspaceIds.Contains(workspace.Id));
        modelBuilder.Entity<ContextItem>()
            .HasQueryFilter(context =>
                HasUnrestrictedWorkspaceAccess || AccessibleWorkspaceIds.Contains(context.WorkspaceId));
        modelBuilder.Entity<Document>()
            .HasQueryFilter(document =>
                HasUnrestrictedWorkspaceAccess || AccessibleWorkspaceIds.Contains(document.WorkspaceId));
        modelBuilder.Entity<DocumentChunk>()
            .HasQueryFilter(chunk =>
                HasUnrestrictedWorkspaceAccess || AccessibleWorkspaceIds.Contains(chunk.WorkspaceId));
    }
}
