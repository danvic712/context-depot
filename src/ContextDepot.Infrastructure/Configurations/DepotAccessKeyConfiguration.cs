using ContextDepot.Domain.Depots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class DepotAccessKeyConfiguration : IEntityTypeConfiguration<DepotAccessKey>
{
    public void Configure(EntityTypeBuilder<DepotAccessKey> entity)
    {
        entity.ToTable("depot_access_keys");
        entity.HasKey(x => x.Id).HasName("pk_depot_access_keys");
        entity.HasAlternateKey(x => new { x.Id, x.DepotId })
            .HasName("ak_depot_access_keys_id_depot_id");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.DepotId).HasColumnName("depot_id");
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(32).IsRequired();
        entity.Property(x => x.SecretHash).HasColumnName("secret_hash").HasMaxLength(128).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        entity.HasIndex(x => x.KeyPrefix)
            .IsUnique()
            .HasDatabaseName("ux_depot_access_keys_key_prefix");
        entity.HasIndex(x => new { x.DepotId, x.Name })
            .HasDatabaseName("ix_depot_access_keys_depot_name");
        entity.HasOne(x => x.Depot)
            .WithMany(x => x.AccessKeys)
            .HasForeignKey(x => x.DepotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_depot_access_keys_depot_id");
    }
}
