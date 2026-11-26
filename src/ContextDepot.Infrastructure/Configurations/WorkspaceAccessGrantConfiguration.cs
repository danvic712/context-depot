using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class WorkspaceAccessGrantConfiguration : IEntityTypeConfiguration<WorkspaceAccessGrant>
{
    public void Configure(EntityTypeBuilder<WorkspaceAccessGrant> entity)
    {
        entity.ToTable("workspace_access_grants");
        entity.HasKey(x => new { x.AccessKeyId, x.WorkspaceId })
            .HasName("pk_workspace_access_grants");
        entity.Property(x => x.AccessKeyId).HasColumnName("access_key_id");
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        entity.HasIndex(x => x.AccessKeyId)
            .HasDatabaseName("ix_workspace_access_grants_access_key");
        entity.HasIndex(x => x.WorkspaceId)
            .HasDatabaseName("ix_workspace_access_grants_workspace");
        entity.HasOne(x => x.AccessKey)
            .WithMany(x => x.WorkspaceGrants)
            .HasForeignKey(x => x.AccessKeyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspace_access_grants_access_key");
        entity.HasOne(x => x.Workspace)
            .WithMany(x => x.AccessKeyGrants)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspace_access_grants_workspace");
    }
}
