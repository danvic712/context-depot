using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ContextDepotDbContext))]
[Migration("20260919230000_EnablePgvector")]
public partial class EnablePgvector : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The extension can be shared by other vector collections and is intentionally retained on rollback.
    }
}
