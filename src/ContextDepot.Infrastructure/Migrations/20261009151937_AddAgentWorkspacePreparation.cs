using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWorkspacePreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "access_version",
                schema: "public",
                table: "depot_access_keys",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "can_create_root_workspaces",
                schema: "public",
                table: "depot_access_keys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "workspace_subject_bindings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_subject_bindings", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_subject_bindings_depot",
                        column: x => x.depot_id,
                        principalSchema: "public",
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workspace_subject_bindings_workspace",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_depot_access_keys_access_version",
                schema: "public",
                table: "depot_access_keys",
                sql: "access_version >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_subject_bindings_workspace",
                schema: "public",
                table: "workspace_subject_bindings",
                columns: new[] { "workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_workspace_subject_bindings_subject",
                schema: "public",
                table: "workspace_subject_bindings",
                columns: new[] { "depot_id", "subject_type", "subject_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_subject_bindings",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_depot_access_keys_access_version",
                schema: "public",
                table: "depot_access_keys");

            migrationBuilder.DropColumn(
                name: "access_version",
                schema: "public",
                table: "depot_access_keys");

            migrationBuilder.DropColumn(
                name: "can_create_root_workspaces",
                schema: "public",
                table: "depot_access_keys");
        }
    }
}
