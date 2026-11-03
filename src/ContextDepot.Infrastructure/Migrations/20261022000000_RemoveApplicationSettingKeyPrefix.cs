using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations;

public partial class RemoveApplicationSettingKeyPrefix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE public.application_settings
            SET key = regexp_replace(key, '^ContextDepot:', '', 'i')
            WHERE key ~* '^ContextDepot:';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE public.application_settings
            SET key = 'ContextDepot:' || key
            WHERE key ~* '^(Retrieval|IndexRepair|VectorCoverage|Appearance):';
            """);
    }
}
