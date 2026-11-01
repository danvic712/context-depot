using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations;

public partial class AddAgentSessionSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "public",
            table: "application_settings",
            columns: new[] { "id", "key", "updated_at", "value" },
            columnTypes: new[] { "uuid", "text", "timestamp with time zone", "jsonb" },
            values: new object[] { new Guid("01995f60-0000-7000-8000-000000000015"), "AgentSessions:Enabled",
                new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero), "false" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "public", table: "application_settings", keyColumn: "id",
            keyColumnType: "uuid", keyValue: new Guid("01995f60-0000-7000-8000-000000000015"));
    }
}
