using ContextDepot.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> entity)
    {
        entity.ToTable("documents");
        entity.HasKey(x => x.Id).HasName("pk_documents");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.OwnerId).HasColumnName("owner_id");
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.Path).HasColumnName("path").HasMaxLength(500).IsRequired();
        entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        entity.Property(x => x.IndexedContentHash).HasColumnName("indexed_content_hash").HasMaxLength(64);
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.IndexStatus).HasColumnName("index_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.LastIndexError).HasColumnName("last_index_error");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.Path })
            .IsUnique()
            .HasDatabaseName("ux_documents_owner_workspace_path");
        entity.HasAlternateKey(x => new { x.Id, x.OwnerId, x.WorkspaceId }).HasName("ak_documents_id_owner_id_workspace_id");
        entity.HasOne(x => x.Workspace)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => new { x.WorkspaceId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_documents_workspaces_workspace_id_owner_id");
    }
}
