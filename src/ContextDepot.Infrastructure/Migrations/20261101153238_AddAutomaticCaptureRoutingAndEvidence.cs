using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticCaptureRoutingAndEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "candidate_outcomes_json",
                schema: "public",
                table: "session_capture_events",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "capture_expires_at",
                schema: "public",
                table: "session_capture_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "content_purged_at",
                schema: "public",
                table: "session_capture_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encrypted_extraction_plan",
                schema: "public",
                table: "session_capture_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extraction_plan_hash",
                schema: "public",
                table: "session_capture_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extractor_version",
                schema: "public",
                table: "session_capture_events",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error_code",
                schema: "public",
                table: "session_capture_events",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_provider_call_at",
                schema: "public",
                table: "session_capture_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                schema: "public",
                table: "session_capture_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "persistence_attempt_count",
                schema: "public",
                table: "session_capture_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "provider_call_count",
                schema: "public",
                table: "session_capture_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preserve pre-upgrade provider usage conservatively for the first hour.
            // Worker shutdown is required before applying this migration.
            migrationBuilder.Sql("""
                UPDATE public.session_capture_events
                SET provider_call_count = attempt_count,
                    last_provider_call_at = CASE WHEN attempt_count > 0 THEN CURRENT_TIMESTAMP ELSE NULL END
                WHERE phase = 'user';
                """);

            migrationBuilder.CreateTable(
                name: "context_capture_evidence",
                schema: "public",
                columns: table => new
                {
                    context_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assistant_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_index = table.Column<int>(type: "integer", nullable: false),
                    user_content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    encrypted_fragments = table.Column<string>(type: "text", nullable: false),
                    fragments_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    extractor_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    protection_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_capture_evidence", x => x.context_id);
                    table.CheckConstraint("ck_capture_evidence_hashes", "user_content_hash ~ '^[0-9A-F]{64}$' AND fragments_hash ~ '^[0-9A-F]{64}$'");
                    table.CheckConstraint("ck_capture_evidence_index", "candidate_index BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_capture_evidence_protection", "protection_version >= 0");
                    table.ForeignKey(
                        name: "FK_context_capture_evidence_context_items_context_id_depot_id_~",
                        columns: x => new { x.context_id, x.depot_id, x.workspace_id },
                        principalSchema: "public",
                        principalTable: "context_items",
                        principalColumns: new[] { "id", "depot_id", "workspace_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_context_capture_evidence_depot_access_keys_access_key_id_de~",
                        columns: x => new { x.access_key_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "depot_access_keys",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_capture_turn_routes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    turn_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    resolver_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    provider_call_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_provider_call_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    promoted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    encrypted_routing_vector = table.Column<string>(type: "text", nullable: true),
                    routing_vector_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    routing_embedding_fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_capture_turn_routes", x => x.id);
                    table.UniqueConstraint("AK_session_capture_turn_routes_id_depot_id_access_key_id_agent~", x => new { x.id, x.depot_id, x.access_key_id, x.agent, x.client_session_id, x.turn_id });
                    table.CheckConstraint("ck_capture_route_counts", "attempt_count >= 0 AND provider_call_count >= 0");
                    table.CheckConstraint("ck_capture_route_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_capture_route_hash", "routing_vector_hash IS NULL OR routing_vector_hash ~ '^[0-9A-F]{64}$'");
                    table.CheckConstraint("ck_capture_route_lease", "(state = 'resolving' AND lease_token IS NOT NULL AND lease_until IS NOT NULL) OR (state <> 'resolving' AND lease_token IS NULL AND lease_until IS NULL)");
                    table.CheckConstraint("ck_capture_route_state", "state IN ('waiting_user','pending','resolving','resolved','promoted','skipped','failed')");
                    table.CheckConstraint("ck_capture_route_vector", "(encrypted_routing_vector IS NULL AND routing_vector_hash IS NULL AND routing_embedding_fingerprint IS NULL) OR (encrypted_routing_vector IS NOT NULL AND routing_vector_hash IS NOT NULL AND routing_embedding_fingerprint IS NOT NULL)");
                    table.CheckConstraint("ck_capture_route_workspace", "state NOT IN ('resolved','promoted') OR workspace_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_session_capture_turn_routes_depot_access_keys_access_key_id~",
                        columns: x => new { x.access_key_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "depot_access_keys",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_capture_turn_routes_workspaces_workspace_id_depot_id",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_capture_inbox_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    turn_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    encrypted_content = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_capture_inbox_events", x => x.id);
                    table.CheckConstraint("ck_capture_inbox_content", "(encrypted_content IS NULL) = (content_purged_at IS NOT NULL)");
                    table.CheckConstraint("ck_capture_inbox_hash", "content_hash ~ '^[0-9A-F]{64}$'");
                    table.CheckConstraint("ck_capture_inbox_phase", "phase IN ('user','assistant')");
                    table.ForeignKey(
                        name: "FK_session_capture_inbox_events_session_capture_turn_routes_tu~",
                        columns: x => new { x.turn_route_id, x.depot_id, x.access_key_id, x.agent, x.client_session_id, x.turn_id },
                        principalSchema: "public",
                        principalTable: "session_capture_turn_routes",
                        principalColumns: new[] { "id", "depot_id", "access_key_id", "agent", "client_session_id", "turn_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_depot_id_access_key_id_last_provider~",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "depot_id", "access_key_id", "last_provider_call_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_capture_event_counts",
                schema: "public",
                table: "session_capture_events",
                sql: "attempt_count >= 0 AND provider_call_count >= 0 AND persistence_attempt_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_capture_event_outcomes",
                schema: "public",
                table: "session_capture_events",
                sql: "jsonb_typeof(candidate_outcomes_json) = 'array' AND jsonb_array_length(candidate_outcomes_json) <= 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_capture_event_plan",
                schema: "public",
                table: "session_capture_events",
                sql: "(encrypted_extraction_plan IS NULL) = (extraction_plan_hash IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_capture_event_plan_hash",
                schema: "public",
                table: "session_capture_events",
                sql: "extraction_plan_hash IS NULL OR extraction_plan_hash ~ '^[0-9A-F]{64}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_capture_event_user_fields",
                schema: "public",
                table: "session_capture_events",
                sql: "phase = 'user' OR (encrypted_extraction_plan IS NULL AND extraction_plan_hash IS NULL AND extractor_version IS NULL AND candidate_outcomes_json = '[]'::jsonb AND provider_call_count = 0 AND last_provider_call_at IS NULL AND persistence_attempt_count = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_context_capture_evidence_access_key_id_depot_id",
                schema: "public",
                table: "context_capture_evidence",
                columns: new[] { "access_key_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_context_capture_evidence_context_id_depot_id_workspace_id",
                schema: "public",
                table: "context_capture_evidence",
                columns: new[] { "context_id", "depot_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "IX_context_capture_evidence_depot_id_access_key_id_user_event_~",
                schema: "public",
                table: "context_capture_evidence",
                columns: new[] { "depot_id", "access_key_id", "user_event_id", "candidate_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_inbox_events_depot_id_access_key_id_agent_c~",
                schema: "public",
                table: "session_capture_inbox_events",
                columns: new[] { "depot_id", "access_key_id", "agent", "client_session_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_inbox_events_turn_route_id",
                schema: "public",
                table: "session_capture_inbox_events",
                column: "turn_route_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_inbox_events_turn_route_id_depot_id_access_~",
                schema: "public",
                table: "session_capture_inbox_events",
                columns: new[] { "turn_route_id", "depot_id", "access_key_id", "agent", "client_session_id", "turn_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_inbox_events_turn_route_id_phase",
                schema: "public",
                table: "session_capture_inbox_events",
                columns: new[] { "turn_route_id", "phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_access_key_id_depot_id",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "access_key_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_depot_id_access_key_id_agent_cl~",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "depot_id", "access_key_id", "agent", "client_session_id", "turn_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_depot_id_access_key_id_last_pro~",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "depot_id", "access_key_id", "last_provider_call_at" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_expires_at_id",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "expires_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_state_lease_until_id",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "state", "lease_until", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_state_next_attempt_at_id",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "state", "next_attempt_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_workspace_id_depot_id",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "workspace_id", "depot_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "context_capture_evidence",
                schema: "public");

            migrationBuilder.DropTable(
                name: "session_capture_inbox_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "session_capture_turn_routes",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_events_depot_id_access_key_id_last_provider~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_capture_event_counts",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_capture_event_outcomes",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_capture_event_plan",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_capture_event_plan_hash",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_capture_event_user_fields",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "candidate_outcomes_json",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "capture_expires_at",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "content_purged_at",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "encrypted_extraction_plan",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "extraction_plan_hash",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "extractor_version",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "last_error_code",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "last_provider_call_at",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "lease_token",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "persistence_attempt_count",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "provider_call_count",
                schema: "public",
                table: "session_capture_events");
        }
    }
}
