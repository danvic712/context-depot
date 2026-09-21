using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class WorkspaceAccessGrantConfiguration : IEntityTypeConfiguration<WorkspaceAccessGrant>
{
    public void Configure(EntityTypeBuilder<WorkspaceAccessGrant> entity)
    {
        entity.ToTable("workspace_access_grants");
        entity.HasKey(x => new { x.DepotAccessKeyId, x.WorkspaceId })
            .HasName("pk_workspace_access_grants");
        entity.Property(x => x.DepotAccessKeyId).HasColumnName("depot_access_key_id");
        entity.Property(x => x.DepotId).HasColumnName("depot_id");
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        entity.HasIndex(x => new { x.DepotAccessKeyId, x.DepotId })
            .HasDatabaseName("ix_workspace_access_grants_access_key_depot");
        entity.HasIndex(x => new { x.WorkspaceId, x.DepotId })
            .HasDatabaseName("ix_workspace_access_grants_workspace_depot");
        entity.HasOne(x => x.AccessKey)
            .WithMany(x => x.WorkspaceGrants)
            .HasForeignKey(x => new { x.DepotAccessKeyId, x.DepotId })
            .HasPrincipalKey(x => new { x.Id, x.DepotId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspace_access_grants_access_key");
        entity.HasOne(x => x.Workspace)
            .WithMany(x => x.AccessKeyGrants)
            .HasForeignKey(x => new { x.WorkspaceId, x.DepotId })
            .HasPrincipalKey(x => new { x.Id, x.DepotId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspace_access_grants_workspace");
    }
}
