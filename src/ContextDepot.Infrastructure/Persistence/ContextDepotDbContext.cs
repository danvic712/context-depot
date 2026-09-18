using ContextDepot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Persistence;

public sealed class ContextDepotDbContext(DbContextOptions<ContextDepotDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<ContextItem> ContextItems => Set<ContextItem>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Owner>(entity =>
        {
            entity.ToTable("owners");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("workspaces");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.ParentWorkspaceId).HasColumnName("parent_workspace_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.Id, x.OwnerId }).IsUnique().HasDatabaseName("ux_workspaces_id_owner");
            entity.HasIndex(x => new { x.OwnerId, x.Slug }).IsUnique().HasDatabaseName("ux_workspaces_root_slug").HasFilter("parent_workspace_id IS NULL");
            entity.HasIndex(x => new { x.OwnerId, x.ParentWorkspaceId, x.Slug }).IsUnique().HasDatabaseName("ux_workspaces_child_slug").HasFilter("parent_workspace_id IS NOT NULL");
            entity.HasOne(x => x.Owner).WithMany(x => x.Workspaces).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ParentWorkspace).WithMany(x => x.Children).HasForeignKey(x => new { x.ParentWorkspaceId, x.OwnerId }).HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContextItem>(entity =>
        {
            entity.ToTable("context_items", table =>
            {
                table.HasCheckConstraint("ck_context_items_importance", "importance BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_context_items_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
            });
            entity.HasKey(x => x.Id);
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
            entity.HasIndex(x => new { x.Id, x.OwnerId, x.WorkspaceId }).IsUnique().HasDatabaseName("ux_context_items_id_owner_workspace");
            entity.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.Key }).HasDatabaseName("ix_context_items_owner_workspace_key");
            entity.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.Key }).IsUnique().HasDatabaseName("ux_context_items_active_key").HasFilter("status = 'Active' AND key IS NOT NULL");
            entity.HasOne(x => x.Workspace).WithMany(x => x.ContextItems).HasForeignKey(x => new { x.WorkspaceId, x.OwnerId }).HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supersedes).WithMany().HasForeignKey(x => new { x.SupersedesId, x.OwnerId, x.WorkspaceId }).HasPrincipalKey(x => new { x.Id, x.OwnerId, x.WorkspaceId }).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(x => x.Id);
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
            entity.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.Path }).IsUnique().HasDatabaseName("ux_documents_owner_workspace_path");
            entity.HasIndex(x => new { x.Id, x.OwnerId, x.WorkspaceId }).IsUnique().HasDatabaseName("ux_documents_id_owner_workspace");
            entity.HasOne(x => x.Workspace).WithMany(x => x.Documents).HasForeignKey(x => new { x.WorkspaceId, x.OwnerId }).HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.DocumentId).HasColumnName("document_id");
            entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(x => x.Ordinal).HasColumnName("ordinal");
            entity.Property(x => x.HeadingPath).HasColumnName("heading_path").HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Content).HasColumnName("content").IsRequired();
            entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique().HasDatabaseName("ux_document_chunks_document_ordinal");
            entity.HasIndex(x => new { x.Id, x.OwnerId, x.WorkspaceId }).IsUnique().HasDatabaseName("ux_document_chunks_id_owner_workspace");
            entity.HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => new { x.DocumentId, x.OwnerId, x.WorkspaceId }).HasPrincipalKey(x => new { x.Id, x.OwnerId, x.WorkspaceId }).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
