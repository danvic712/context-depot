using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations;

public partial class RemoveIndexRepairPollInterval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM public.application_settings
            WHERE normalized_key = 'indexrepair:pollintervalseconds';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO public.application_settings (id, key, updated_at, value)
            VALUES ('01995f60-0000-7000-8000-000000000009'::uuid,
                    'IndexRepair:PollIntervalSeconds',
                    TIMESTAMPTZ '2026-11-02 00:00:00+00', '30'::jsonb)
            ON CONFLICT DO NOTHING;
            """);
    }
}
