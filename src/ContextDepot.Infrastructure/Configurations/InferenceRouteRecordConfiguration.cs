using ContextDepot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class InferenceRouteRecordConfiguration : IEntityTypeConfiguration<InferenceRouteRecord>
{
    public void Configure(EntityTypeBuilder<InferenceRouteRecord> entity)
    {
        entity.ToTable("inference_routes", table =>
        {
            table.HasCheckConstraint(
                "ck_inference_routes_capability",
                "capability IN ('chat', 'embedding')");
            table.HasCheckConstraint(
                "ck_inference_routes_provider_model_pair",
                "(provider_id IS NULL AND model_name IS NULL) OR (provider_id IS NOT NULL AND model_name IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_inference_routes_dimensions",
                "(capability = 'chat' AND dimensions IS NULL) OR (capability = 'embedding' AND (model_name IS NULL OR dimensions > 0))");
            table.HasCheckConstraint(
                "ck_inference_routes_index_generation",
                "index_generation >= 0");
            table.HasCheckConstraint(
                "ck_inference_routes_timeout_seconds",
                "timeout_seconds BETWEEN 1 AND 300");
        });
        entity.HasKey(route => route.Id).HasName("pk_inference_routes");
        entity.Property(route => route.Id).HasColumnName("id");
        entity.Property(route => route.Capability).HasColumnName("capability").HasMaxLength(20).IsRequired();
        entity.Property(route => route.ProviderId).HasColumnName("provider_id");
        entity.Property(route => route.ModelName).HasColumnName("model_name").HasMaxLength(300);
        entity.Property(route => route.Dimensions).HasColumnName("dimensions");
        entity.Property(route => route.TimeoutSeconds).HasColumnName("timeout_seconds");
        entity.Property(route => route.EmbeddingProfileFingerprint).HasColumnName("embedding_profile_fingerprint").HasMaxLength(64);
        entity.Property(route => route.IndexState).HasColumnName("index_state").HasMaxLength(30).IsRequired();
        entity.Property(route => route.IndexGeneration).HasColumnName("index_generation");
        entity.Property(route => route.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(route => route.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(route => route.Capability)
            .IsUnique()
            .HasDatabaseName("ux_inference_routes_capability");
        entity.HasOne(route => route.Provider)
            .WithMany(provider => provider.Routes)
            .HasForeignKey(route => route.ProviderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_inference_routes_provider");

        entity.HasData(
            new
            {
                Id = Guid.Parse("01995f60-0000-7000-8000-000000000101"),
                Capability = "chat",
                ProviderId = (Guid?)null,
                ModelName = (string?)null,
                Dimensions = (int?)null,
                TimeoutSeconds = 60,
                EmbeddingProfileFingerprint = (string?)null,
                IndexState = "unconfigured",
                IndexGeneration = 0L,
                CreatedAt = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero)
            },
            new
            {
                Id = Guid.Parse("01995f60-0000-7000-8000-000000000102"),
                Capability = "embedding",
                ProviderId = (Guid?)null,
                ModelName = (string?)null,
                Dimensions = (int?)null,
                TimeoutSeconds = 60,
                EmbeddingProfileFingerprint = (string?)null,
                IndexState = "unconfigured",
                IndexGeneration = 0L,
                CreatedAt = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero)
            });
    }
}
