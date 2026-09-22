using ContextDepot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class ApplicationSettingRecordConfiguration : IEntityTypeConfiguration<ApplicationSettingRecord>
{
    public void Configure(EntityTypeBuilder<ApplicationSettingRecord> entity)
    {
        entity.ToTable("application_settings", table => table.HasCheckConstraint(
            "ck_application_settings_value_scalar",
            "jsonb_typeof(value) IN ('string', 'number', 'boolean')"));
        entity.HasKey(setting => setting.Id).HasName("pk_application_settings");
        entity.Property(setting => setting.Id).HasColumnName("id");
        entity.Property(setting => setting.Key).HasColumnName("key").IsRequired();
        entity.Property(setting => setting.ValueJson).HasColumnName("value").HasColumnType("jsonb").IsRequired();
        entity.Property(setting => setting.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        entity.Property<string>("NormalizedKey")
            .HasColumnName("normalized_key")
            .HasComputedColumnSql("lower(key)", stored: true);
        entity.HasIndex("NormalizedKey")
            .IsUnique()
            .HasDatabaseName("ux_application_settings_normalized_key");

        entity.HasData(
            Seed("01995f60-0000-7000-8000-000000000001", "ContextDepot:Retrieval:Semantic:ScopeTopK", "8"),
            Seed("01995f60-0000-7000-8000-000000000002", "ContextDepot:Retrieval:Semantic:CandidateTopKPerSource", "20"),
            Seed("01995f60-0000-7000-8000-000000000003", "ContextDepot:Retrieval:Semantic:OversampleFactor", "3"),
            Seed("01995f60-0000-7000-8000-000000000004", "ContextDepot:Retrieval:Semantic:RetrievalLexicalFallbackThreshold", "0.65"),
            Seed("01995f60-0000-7000-8000-000000000005", "ContextDepot:Retrieval:Semantic:DedupSimilarityThreshold", "0.98"),
            Seed("01995f60-0000-7000-8000-000000000006", "ContextDepot:Retrieval:Semantic:DedupTokenOverlapThreshold", "0.8"),
            Seed("01995f60-0000-7000-8000-000000000007", "ContextDepot:Retrieval:Search:DefaultLimit", "10"),
            Seed("01995f60-0000-7000-8000-000000000008", "ContextDepot:Retrieval:Search:MaxLimit", "50"),
            Seed("01995f60-0000-7000-8000-000000000009", "ContextDepot:IndexRepair:PollIntervalSeconds", "30"),
            Seed("01995f60-0000-7000-8000-000000000010", "ContextDepot:IndexRepair:BatchSize", "32"),
            Seed("01995f60-0000-7000-8000-000000000011", "ContextDepot:IndexRepair:MaxBatchesPerCycle", "4"),
            Seed("01995f60-0000-7000-8000-000000000012", "ContextDepot:VectorCoverage:CacheDurationSeconds", "30"),
            Seed("01995f60-0000-7000-8000-000000000013", "ContextDepot:Appearance:Language", "\"zh-CN\""),
            Seed("01995f60-0000-7000-8000-000000000014", "ContextDepot:Appearance:Theme", "\"system\""));
    }

    private static ApplicationSettingRecord Seed(string id, string key, string valueJson) => new()
    {
        Id = Guid.Parse(id),
        Key = key,
        ValueJson = valueJson,
        UpdatedAt = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero)
    };
}
