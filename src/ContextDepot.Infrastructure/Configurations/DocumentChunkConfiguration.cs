using ContextDepot.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> entity)
    {
        entity.ToTable("document_chunks");
        entity.HasKey(x => x.Id).HasName("pk_document_chunks");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.DepotId).HasColumnName("depot_id");
        entity.Property(x => x.DocumentId).HasColumnName("document_id");
        entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
        entity.Property(x => x.Ordinal).HasColumnName("ordinal");
        entity.Property(x => x.HeadingPath).HasColumnName("heading_path").HasMaxLength(1000).IsRequired();
        entity.Property(x => x.Content).HasColumnName("content").IsRequired();
        entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique().HasDatabaseName("ux_document_chunks_document_ordinal");
        entity.HasIndex(x => new { x.Id, x.DepotId, x.WorkspaceId }).IsUnique().HasDatabaseName("ux_document_chunks_id_depot_workspace");
        entity.HasOne(x => x.Document)
            .WithMany(x => x.Chunks)
            .HasForeignKey(x => new { x.DocumentId, x.DepotId, x.WorkspaceId })
            .HasPrincipalKey(x => new { x.Id, x.DepotId, x.WorkspaceId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_document_chunks_documents_document_id_depot_id_workspace_id");
    }
}
