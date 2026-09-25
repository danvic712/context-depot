using ContextDepot.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContextDepot.Infrastructure.Configurations;

public sealed class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> entity)
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
    }
}
