using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContextLifecycleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_context_items_owner_workspace_key",
                schema: "public",
                table: "context_items");

            migrationBuilder.CreateIndex(
                name: "ux_context_items_active_key",
                schema: "public",
                table: "context_items",
                columns: new[] { "owner_id", "workspace_id", "key" },
                unique: true,
                filter: "status = 'Active' AND key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_context_items_active_key",
                schema: "public",
                table: "context_items");

            migrationBuilder.CreateIndex(
                name: "ix_context_items_owner_workspace_key",
                schema: "public",
                table: "context_items",
                columns: new[] { "owner_id", "workspace_id", "key" });
        }
    }
}
