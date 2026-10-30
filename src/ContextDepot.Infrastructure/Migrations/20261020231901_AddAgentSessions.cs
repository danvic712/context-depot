using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                schema: "public",
                table: "session_capture_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agent_sessions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_sessions", x => x.id);
                    table.UniqueConstraint("AK_agent_sessions_id_depot_id_access_key_id_workspace_id", x => new { x.id, x.depot_id, x.access_key_id, x.workspace_id });
                    table.CheckConstraint("ck_agent_session_ended", "(ended_at IS NULL AND end_reason IS NULL) OR (ended_at IS NOT NULL AND end_reason IS NOT NULL AND end_reason IN ('completed', 'interrupted', 'other'))");
                    table.ForeignKey(
                        name: "FK_agent_sessions_depot_access_keys_access_key_id_depot_id",
                        columns: x => new { x.access_key_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "depot_access_keys",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_workspaces_workspace_id_depot_id",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_session_id_depot_id_access_key_id_wo~",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "session_id", "depot_id", "access_key_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_access_key_id_depot_id",
                schema: "public",
                table: "agent_sessions",
                columns: new[] { "access_key_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_workspace_id_depot_id",
                schema: "public",
                table: "agent_sessions",
                columns: new[] { "workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_agent_session_client",
                schema: "public",
                table: "agent_sessions",
                columns: new[] { "depot_id", "access_key_id", "agent", "client_session_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_events_agent_sessions_session_id_depot_id_a~",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "session_id", "depot_id", "access_key_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "agent_sessions",
                principalColumns: new[] { "id", "depot_id", "access_key_id", "workspace_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_events_agent_sessions_session_id_depot_id_a~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropTable(
                name: "agent_sessions",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_events_session_id_depot_id_access_key_id_wo~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "session_id",
                schema: "public",
                table: "session_capture_events");
        }
    }
}
