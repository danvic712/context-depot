using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class InstallationStateConfiguration : IEntityTypeConfiguration<InstallationState>
{
    public void Configure(EntityTypeBuilder<InstallationState> entity)
    {
        entity.ToTable("installation_state", table =>
        {
            table.HasCheckConstraint("ck_installation_state_scope", "scope = 'installation'");
            table.HasCheckConstraint("ck_installation_state_state", "state IN ('pending', 'inProgress', 'completed')");
        });
        entity.HasKey(state => state.Id).HasName("pk_installation_state");
        entity.Property(state => state.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(state => state.Scope).HasColumnName("scope").HasMaxLength(30).IsRequired();
        entity.Property(state => state.State).HasColumnName("state").HasMaxLength(20).IsRequired();
        entity.Property(state => state.InitialDepotId).HasColumnName("initial_depot_id");
        entity.Property(state => state.InitialWorkspaceId).HasColumnName("initial_workspace_id");
        entity.Property(state => state.InferenceReviewed).HasColumnName("inference_reviewed");
        entity.Property(state => state.AccessKeyReviewed).HasColumnName("access_key_reviewed");
        entity.Property(state => state.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        entity.Property(state => state.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(state => state.Scope).IsUnique().HasDatabaseName("ux_installation_state_scope");
        entity.HasIndex(state => state.InitialDepotId).HasDatabaseName("ix_installation_state_initial_depot_id");
        entity.HasIndex(state => new { state.InitialWorkspaceId, state.InitialDepotId })
            .HasDatabaseName("ix_installation_state_initial_workspace_id_initial_depot_id");
        entity.HasOne<Depot>().WithMany().HasForeignKey(state => state.InitialDepotId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_installation_state_depots_initial_depot_id");
        entity.HasOne<Workspace>().WithMany().HasForeignKey(state => new { state.InitialWorkspaceId, state.InitialDepotId })
            .HasPrincipalKey(workspace => new { workspace.Id, workspace.DepotId }).OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_installation_state_workspaces_initial_workspace_id_depot_id");
    }
}
