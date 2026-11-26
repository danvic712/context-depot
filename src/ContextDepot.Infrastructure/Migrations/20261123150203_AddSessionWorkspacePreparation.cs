using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionWorkspacePreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encrypted_extraction_plan",
                schema: "public",
                table: "session_capture_turn_routes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extraction_plan_hash",
                schema: "public",
                table: "session_capture_turn_routes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_capture_route_plan",
                schema: "public",
                table: "session_capture_turn_routes",
                sql: "(encrypted_extraction_plan IS NULL AND extraction_plan_hash IS NULL) OR (state = 'resolved' AND encrypted_extraction_plan IS NOT NULL AND extraction_plan_hash IS NOT NULL AND extraction_plan_hash ~ '^[0-9A-F]{64}$')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_capture_route_plan",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropColumn(
                name: "encrypted_extraction_plan",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropColumn(
                name: "extraction_plan_hash",
                schema: "public",
                table: "session_capture_turn_routes");
        }
    }
}
