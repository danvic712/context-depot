using ContextDepot.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class AccessKeyConfiguration : IEntityTypeConfiguration<AccessKey>
{
    public void Configure(EntityTypeBuilder<AccessKey> entity)
    {
        entity.ToTable("access_keys", table => table.HasCheckConstraint("ck_access_keys_access_version", "access_version >= 0"));
        entity.HasKey(x => x.Id).HasName("pk_access_keys");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(32).IsRequired();
        entity.Property(x => x.SecretHash).HasColumnName("secret_hash").HasMaxLength(128).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        entity.Property(x => x.CanCreateRootWorkspaces).HasColumnName("can_create_root_workspaces").HasDefaultValue(false);
        entity.Property(x => x.AccessVersion).HasColumnName("access_version").HasDefaultValue(0L);
        entity.HasIndex(x => x.KeyPrefix)
            .IsUnique()
            .HasDatabaseName("ux_access_keys_key_prefix");
        entity.HasIndex(x => new { x.Name })
            .HasDatabaseName("ix_access_keys_name");
    }
}
