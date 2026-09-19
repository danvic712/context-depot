using ContextDepot.Domain.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class ContextItemConfiguration : IEntityTypeConfiguration<ContextItem>
{
    public void Configure(EntityTypeBuilder<ContextItem> entity)
    {
        entity.ToTable("context_items", table =>
        {
            table.HasCheckConstraint("ck_context_items_importance", "importance BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_context_items_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
        });
        entity.HasKey(x => x.Id).HasName("pk_context_items");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.OwnerId).HasColumnName("owner_id");
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.Key).HasColumnName("key").HasMaxLength(200);
        entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
        entity.Property(x => x.Content).HasColumnName("content").IsRequired();
        entity.Property(x => x.TagsJson).HasColumnName("tags").HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.Importance).HasColumnName("importance").HasDefaultValue((short)50);
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.ProvenanceTrust).HasColumnName("provenance_trust").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        entity.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.SourceAgent).HasColumnName("source_agent").HasMaxLength(100);
        entity.Property(x => x.SourceRef).HasColumnName("source_ref").HasMaxLength(500);
        entity.Property(x => x.SupersedesId).HasColumnName("supersedes_id");
        entity.Property(x => x.ValidFrom).HasColumnName("valid_from").HasColumnType("timestamp with time zone");
        entity.Property(x => x.ValidUntil).HasColumnName("valid_until").HasColumnType("timestamp with time zone");
        entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.Sensitivity).HasColumnName("sensitivity").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasAlternateKey(x => new { x.Id, x.OwnerId, x.WorkspaceId }).HasName("ak_context_items_id_owner_id_workspace_id");
        entity.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.Key }).HasDatabaseName("ix_context_items_owner_workspace_key");
        entity.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.Key })
            .IsUnique()
            .HasDatabaseName("ux_context_items_active_key")
            .HasFilter("status = 'Active' AND key IS NOT NULL");
        entity.HasOne(x => x.Workspace)
            .WithMany(x => x.ContextItems)
            .HasForeignKey(x => new { x.WorkspaceId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_context_items_workspaces_workspace_id_owner_id");
        entity.HasOne(x => x.Supersedes)
            .WithMany()
            .HasForeignKey(x => new { x.SupersedesId, x.OwnerId, x.WorkspaceId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId, x.WorkspaceId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_context_items_supersedes");
    }
}
