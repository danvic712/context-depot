using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installation_state",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    initial_depot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    initial_workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inference_reviewed = table.Column<bool>(type: "boolean", nullable: false),
                    access_key_reviewed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation_state", x => x.id);
                    table.CheckConstraint("ck_installation_state_scope", "scope = 'installation'");
                    table.CheckConstraint("ck_installation_state_state", "state IN ('pending', 'inProgress', 'completed')");
                    table.ForeignKey(
                        name: "fk_installation_state_depots_initial_depot_id",
                        column: x => x.initial_depot_id,
                        principalSchema: "public",
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_installation_state_workspaces_initial_workspace_id_depot_id",
                        columns: x => new { x.initial_workspace_id, x.initial_depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_installation_state_initial_depot_id",
                schema: "public",
                table: "installation_state",
                column: "initial_depot_id");

            migrationBuilder.CreateIndex(
                name: "ix_installation_state_initial_workspace_id_initial_depot_id",
                schema: "public",
                table: "installation_state",
                columns: new[] { "initial_workspace_id", "initial_depot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_installation_state_scope",
                schema: "public",
                table: "installation_state",
                column: "scope",
                unique: true);

            // Existing installations keep all resources and settings and never re-enter the wizard.
            // The constant singleton ID is a UUID v7; scope expresses business uniqueness separately.
            migrationBuilder.Sql("""
                INSERT INTO public.installation_state
                    (id, scope, state, initial_depot_id, initial_workspace_id,
                     inference_reviewed, access_key_reviewed, completed_at, updated_at)
                SELECT '019a3600-0000-7000-8000-000000000001'::uuid, 'installation',
                    CASE WHEN EXISTS (SELECT 1 FROM public.depots) THEN 'completed' ELSE 'pending' END,
                    NULL, NULL,
                    EXISTS (SELECT 1 FROM public.depots), EXISTS (SELECT 1 FROM public.depots),
                    CASE WHEN EXISTS (SELECT 1 FROM public.depots) THEN CURRENT_TIMESTAMP ELSE NULL END,
                    CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installation_state",
                schema: "public");
        }
    }
}
