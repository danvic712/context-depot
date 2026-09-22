using ContextDepot.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class InferenceProviderRecordConfiguration : IEntityTypeConfiguration<InferenceProviderRecord>
{
    public void Configure(EntityTypeBuilder<InferenceProviderRecord> entity)
    {
        entity.ToTable("inference_providers", table => table.HasCheckConstraint(
            "ck_inference_providers_protocol_code",
            "protocol_code = 'openai-compatible'"));
        entity.HasKey(provider => provider.Id).HasName("pk_inference_providers");
        entity.Property(provider => provider.Id).HasColumnName("id");
        entity.Property(provider => provider.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(provider => provider.ProtocolCode).HasColumnName("protocol_code").HasMaxLength(50).IsRequired();
        entity.Property(provider => provider.BaseUrl).HasColumnName("base_url").HasMaxLength(2000).IsRequired();
        entity.Property(provider => provider.ProtectedApiKey).HasColumnName("protected_api_key");
        entity.Property(provider => provider.VerificationState).HasColumnName("verification_state").HasMaxLength(30).IsRequired();
        entity.Property(provider => provider.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(provider => provider.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
    }
}
