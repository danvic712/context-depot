using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> entity)
    {
        entity.ToTable("workspaces");
        entity.HasKey(x => x.Id).HasName("pk_workspaces");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.OwnerId).HasColumnName("owner_id");
        entity.Property(x => x.ParentWorkspaceId).HasColumnName("parent_workspace_id");
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description");
        entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasAlternateKey(x => new { x.Id, x.OwnerId }).HasName("ak_workspaces_id_owner_id");
        entity.HasIndex(x => new { x.OwnerId, x.Slug })
            .IsUnique()
            .HasDatabaseName("ux_workspaces_root_slug")
            .HasFilter("parent_workspace_id IS NULL");
        entity.HasIndex(x => new { x.OwnerId, x.ParentWorkspaceId, x.Slug })
            .IsUnique()
            .HasDatabaseName("ux_workspaces_child_slug")
            .HasFilter("parent_workspace_id IS NOT NULL");
        entity.HasOne(x => x.Owner)
            .WithMany(x => x.Workspaces)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspaces_owners_owner_id");
        entity.HasOne(x => x.ParentWorkspace)
            .WithMany(x => x.Children)
            .HasForeignKey(x => new { x.ParentWorkspaceId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspaces_workspaces_parent_workspace_id_owner_id");
    }
}
