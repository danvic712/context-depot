using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCaptureEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_capture_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    turn_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    encrypted_content = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_capture_events", x => x.id);
                    table.CheckConstraint("ck_session_capture_phase", "phase IN ('user', 'assistant')");
                    table.CheckConstraint("ck_session_capture_status", "status IN ('pending', 'processing', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_session_capture_events_depot_access_keys_access_key_id_depo~",
                        columns: x => new { x.access_key_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "depot_access_keys",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_capture_events_workspaces_workspace_id_depot_id",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_access_key_id_depot_id",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "access_key_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_workspace_id_depot_id",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ix_session_capture_pending",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ux_session_capture_event_identity",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "depot_id", "access_key_id", "agent", "client_session_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_session_capture_turn_phase",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "depot_id", "access_key_id", "agent", "client_session_id", "turn_id", "phase" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_capture_events",
                schema: "public");
        }
    }
}
