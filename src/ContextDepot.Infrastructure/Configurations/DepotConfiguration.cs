using ContextDepot.Domain.Depots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class DepotConfiguration : IEntityTypeConfiguration<Depot>
{
    public void Configure(EntityTypeBuilder<Depot> entity)
    {
        entity.ToTable("depots");
        entity.HasKey(x => x.Id).HasName("pk_depots");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
    }
}
