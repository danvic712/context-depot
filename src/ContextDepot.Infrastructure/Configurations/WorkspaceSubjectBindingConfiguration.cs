using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class WorkspaceSubjectBindingConfiguration : IEntityTypeConfiguration<WorkspaceSubjectBinding>
{
    public void Configure(EntityTypeBuilder<WorkspaceSubjectBinding> entity)
    {
        entity.ToTable("workspace_subject_bindings");
        entity.HasKey(item => item.Id).HasName("pk_workspace_subject_bindings");
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.DepotId).HasColumnName("depot_id");
        entity.Property(item => item.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(item => item.SubjectType).HasColumnName("subject_type").HasMaxLength(32)
            .HasConversion(value => value.ToString().ToLowerInvariant(), value => Enum.Parse<WorkspaceSubjectType>(value, true));
        entity.Property(item => item.SubjectKey).HasColumnName("subject_key").HasMaxLength(200).IsRequired();
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(item => new { item.DepotId, item.SubjectType, item.SubjectKey }).IsUnique()
            .HasDatabaseName("ux_workspace_subject_bindings_subject");
        entity.HasIndex(item => new { item.WorkspaceId, item.DepotId }).HasDatabaseName("ix_workspace_subject_bindings_workspace");
        entity.HasOne<Workspace>().WithMany().HasForeignKey(item => new { item.WorkspaceId, item.DepotId })
            .HasPrincipalKey(item => new { item.Id, item.DepotId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspace_subject_bindings_workspace");
        entity.HasOne<Depot>().WithMany().HasForeignKey(item => item.DepotId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workspace_subject_bindings_depot");
    }
}
