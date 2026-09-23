using Microsoft.EntityFrameworkCore.Migrations;

namespace ContextDepot.Infrastructure.Migrations;

public partial class StopManagingConfigurationSeedData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The preceding migration inserted the initial rows. Keep them intact while
        // removing model-managed seed metadata so future migrations cannot overwrite edits.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverting metadata ownership does not change persisted configuration values.
    }
}
