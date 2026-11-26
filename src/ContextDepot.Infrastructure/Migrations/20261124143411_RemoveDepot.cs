using System;
using ContextDepot.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An installation's data must never be merged with another legacy tenant implicitly.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM public.depots) > 1 THEN
                        RAISE EXCEPTION 'This upgrade supports one legacy Depot per database. Migrate separate Depots independently.';
                    END IF;
                END
                $$;

                ALTER TABLE public.workspaces ADD COLUMN storage_path text;
                WITH RECURSIVE paths AS (
                    SELECT id, replace(depot_id::text, '-', '') || '/' || slug AS path
                    FROM public.workspaces WHERE parent_workspace_id IS NULL
                    UNION ALL
                    SELECT child.id, paths.path || '/' || child.slug
                    FROM public.workspaces child JOIN paths ON child.parent_workspace_id = paths.id
                )
                UPDATE public.workspaces AS workspace
                SET storage_path = paths.path
                FROM paths WHERE paths.id = workspace.id;
                ALTER TABLE public.workspaces ALTER COLUMN storage_path SET NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_depot_access_keys_access_key_id_depot_id",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_workspaces_workspace_id_depot_id",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_context_capture_evidence_context_items_context_id_depot_id_~",
                schema: "public",
                table: "context_capture_evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_context_capture_evidence_depot_access_keys_access_key_id_de~",
                schema: "public",
                table: "context_capture_evidence");

            migrationBuilder.DropForeignKey(
                name: "fk_context_items_supersedes",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropForeignKey(
                name: "fk_context_items_workspaces_workspace_id_depot_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropForeignKey(
                name: "fk_depot_access_keys_depot_id",
                schema: "public",
                table: "depot_access_keys");

            migrationBuilder.DropForeignKey(
                name: "fk_document_chunks_documents_document_id_depot_id_workspace_id",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_workspaces_workspace_id_depot_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_installation_state_depots_initial_depot_id",
                schema: "public",
                table: "installation_state");

            migrationBuilder.DropForeignKey(
                name: "fk_installation_state_workspaces_initial_workspace_id_depot_id",
                schema: "public",
                table: "installation_state");

            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_events_agent_sessions_session_id_depot_id_a~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_events_depot_access_keys_access_key_id_depo~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_events_workspaces_workspace_id_depot_id",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_inbox_events_session_capture_turn_routes_tu~",
                schema: "public",
                table: "session_capture_inbox_events");

            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_turn_routes_depot_access_keys_access_key_id~",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropForeignKey(
                name: "FK_session_capture_turn_routes_workspaces_workspace_id_depot_id",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropForeignKey(
                name: "fk_workspace_access_grants_access_key",
                schema: "public",
                table: "workspace_access_grants");

            migrationBuilder.DropForeignKey(
                name: "fk_workspace_access_grants_workspace",
                schema: "public",
                table: "workspace_access_grants");

            migrationBuilder.DropForeignKey(
                name: "fk_workspace_subject_bindings_depot",
                schema: "public",
                table: "workspace_subject_bindings");

            migrationBuilder.DropForeignKey(
                name: "fk_workspace_subject_bindings_workspace",
                schema: "public",
                table: "workspace_subject_bindings");

            migrationBuilder.DropForeignKey(
                name: "fk_workspaces_depots_depot_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropForeignKey(
                name: "fk_workspaces_workspaces_parent_workspace_id_depot_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropTable(
                name: "depots",
                schema: "public");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_workspaces_id_depot_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "IX_workspaces_parent_workspace_id_depot_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "ux_workspaces_child_slug",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "ux_workspaces_root_slug",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "ix_workspace_subject_bindings_workspace",
                schema: "public",
                table: "workspace_subject_bindings");

            migrationBuilder.DropIndex(
                name: "ux_workspace_subject_bindings_subject",
                schema: "public",
                table: "workspace_subject_bindings");

            migrationBuilder.DropIndex(
                name: "ix_workspace_access_grants_access_key_depot",
                schema: "public",
                table: "workspace_access_grants");

            migrationBuilder.DropIndex(
                name: "ix_workspace_access_grants_workspace_depot",
                schema: "public",
                table: "workspace_access_grants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_session_capture_turn_routes_id_depot_id_access_key_id_agent~",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_turn_routes_access_key_id_depot_id",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_turn_routes_depot_id_access_key_id_agent_cl~",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_turn_routes_depot_id_access_key_id_last_pro~",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_turn_routes_workspace_id_depot_id",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_inbox_events_depot_id_access_key_id_agent_c~",
                schema: "public",
                table: "session_capture_inbox_events");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_inbox_events_turn_route_id_depot_id_access_~",
                schema: "public",
                table: "session_capture_inbox_events");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_events_access_key_id_depot_id",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_events_depot_id_access_key_id_last_provider~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_events_session_id_depot_id_access_key_id_wo~",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropIndex(
                name: "IX_session_capture_events_workspace_id_depot_id",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropIndex(
                name: "ux_session_capture_event_identity",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropIndex(
                name: "ux_session_capture_turn_phase",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropIndex(
                name: "ix_installation_state_initial_depot_id",
                schema: "public",
                table: "installation_state");

            migrationBuilder.DropIndex(
                name: "ix_installation_state_initial_workspace_id_initial_depot_id",
                schema: "public",
                table: "installation_state");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_documents_id_depot_id_workspace_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_workspace_id_depot_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ux_documents_depot_workspace_path",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_document_id_depot_id_workspace_id",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "ux_document_chunks_id_depot_workspace",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_depot_access_keys_id_depot_id",
                schema: "public",
                table: "depot_access_keys");

            migrationBuilder.DropIndex(
                name: "ix_depot_access_keys_depot_name",
                schema: "public",
                table: "depot_access_keys");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_context_items_id_depot_id_workspace_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropIndex(
                name: "IX_context_items_supersedes_id_depot_id_workspace_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropIndex(
                name: "IX_context_items_workspace_id_depot_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropIndex(
                name: "ux_context_items_active_key",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropIndex(
                name: "IX_context_capture_evidence_access_key_id_depot_id",
                schema: "public",
                table: "context_capture_evidence");

            migrationBuilder.DropIndex(
                name: "IX_context_capture_evidence_context_id_depot_id_workspace_id",
                schema: "public",
                table: "context_capture_evidence");

            migrationBuilder.DropIndex(
                name: "IX_context_capture_evidence_depot_id_access_key_id_user_event_~",
                schema: "public",
                table: "context_capture_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_agent_sessions_id_depot_id_access_key_id_workspace_id",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "IX_agent_sessions_access_key_id_depot_id",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "IX_agent_sessions_workspace_id_depot_id",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "ux_agent_session_client",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "workspace_subject_bindings");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "workspace_access_grants");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "session_capture_turn_routes");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "session_capture_inbox_events");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "session_capture_events");

            migrationBuilder.DropColumn(
                name: "initial_depot_id",
                schema: "public",
                table: "installation_state");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "depot_access_keys");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "context_capture_evidence");

            migrationBuilder.DropColumn(
                name: "depot_id",
                schema: "public",
                table: "agent_sessions");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_session_capture_turn_routes_id_access_key_id_agent_client_s~",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "id", "access_key_id", "agent", "client_session_id", "turn_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_documents_id_workspace_id",
                schema: "public",
                table: "documents",
                columns: new[] { "id", "workspace_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_context_items_id_workspace_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "id", "workspace_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_agent_sessions_id_access_key_id_workspace_id",
                schema: "public",
                table: "agent_sessions",
                columns: new[] { "id", "access_key_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_child_slug",
                schema: "public",
                table: "workspaces",
                columns: new[] { "parent_workspace_id", "slug" },
                unique: true,
                filter: "parent_workspace_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_root_slug",
                schema: "public",
                table: "workspaces",
                column: "slug",
                unique: true,
                filter: "parent_workspace_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_subject_bindings_workspace",
                schema: "public",
                table: "workspace_subject_bindings",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ux_workspace_subject_bindings_subject",
                schema: "public",
                table: "workspace_subject_bindings",
                columns: new[] { "subject_type", "subject_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_access_grants_access_key",
                schema: "public",
                table: "workspace_access_grants",
                column: "depot_access_key_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_access_grants_workspace",
                schema: "public",
                table: "workspace_access_grants",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_access_key_id_agent_client_sess~",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "access_key_id", "agent", "client_session_id", "turn_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_access_key_id_last_provider_cal~",
                schema: "public",
                table: "session_capture_turn_routes",
                columns: new[] { "access_key_id", "last_provider_call_at" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_turn_routes_workspace_id",
                schema: "public",
                table: "session_capture_turn_routes",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_inbox_events_access_key_id_agent_client_ses~",
                schema: "public",
                table: "session_capture_inbox_events",
                columns: new[] { "access_key_id", "agent", "client_session_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_inbox_events_turn_route_id_access_key_id_ag~",
                schema: "public",
                table: "session_capture_inbox_events",
                columns: new[] { "turn_route_id", "access_key_id", "agent", "client_session_id", "turn_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_access_key_id_last_provider_call_at",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "access_key_id", "last_provider_call_at" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_session_id_access_key_id_workspace_id",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "session_id", "access_key_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_capture_events_workspace_id",
                schema: "public",
                table: "session_capture_events",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ux_session_capture_event_identity",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "access_key_id", "agent", "client_session_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_session_capture_turn_phase",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "access_key_id", "agent", "client_session_id", "turn_id", "phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installation_state_initial_workspace_id",
                schema: "public",
                table: "installation_state",
                column: "initial_workspace_id");

            migrationBuilder.CreateIndex(
                name: "ux_documents_workspace_path",
                schema: "public",
                table: "documents",
                columns: new[] { "workspace_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_document_id_workspace_id",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "ux_document_chunks_id_workspace",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_depot_access_keys_name",
                schema: "public",
                table: "depot_access_keys",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_context_items_supersedes_id_workspace_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "supersedes_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "ux_context_items_active_key",
                schema: "public",
                table: "context_items",
                columns: new[] { "workspace_id", "key" },
                unique: true,
                filter: "status = 'Active' AND key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_context_capture_evidence_access_key_id_user_event_id_candid~",
                schema: "public",
                table: "context_capture_evidence",
                columns: new[] { "access_key_id", "user_event_id", "candidate_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_context_capture_evidence_context_id_workspace_id",
                schema: "public",
                table: "context_capture_evidence",
                columns: new[] { "context_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_workspace_id",
                schema: "public",
                table: "agent_sessions",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ux_agent_session_client",
                schema: "public",
                table: "agent_sessions",
                columns: new[] { "access_key_id", "agent", "client_session_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_depot_access_keys_access_key_id",
                schema: "public",
                table: "agent_sessions",
                column: "access_key_id",
                principalSchema: "public",
                principalTable: "depot_access_keys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_workspaces_workspace_id",
                schema: "public",
                table: "agent_sessions",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_context_capture_evidence_context_items_context_id_workspace~",
                schema: "public",
                table: "context_capture_evidence",
                columns: new[] { "context_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "context_items",
                principalColumns: new[] { "id", "workspace_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_context_capture_evidence_depot_access_keys_access_key_id",
                schema: "public",
                table: "context_capture_evidence",
                column: "access_key_id",
                principalSchema: "public",
                principalTable: "depot_access_keys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_context_items_supersedes",
                schema: "public",
                table: "context_items",
                columns: new[] { "supersedes_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "context_items",
                principalColumns: new[] { "id", "workspace_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_context_items_workspaces_workspace_id",
                schema: "public",
                table: "context_items",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_chunks_documents_document_id_workspace_id",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "documents",
                principalColumns: new[] { "id", "workspace_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_workspaces_workspace_id",
                schema: "public",
                table: "documents",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_installation_state_workspaces_initial_workspace_id",
                schema: "public",
                table: "installation_state",
                column: "initial_workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_events_agent_sessions_session_id_access_key~",
                schema: "public",
                table: "session_capture_events",
                columns: new[] { "session_id", "access_key_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "agent_sessions",
                principalColumns: new[] { "id", "access_key_id", "workspace_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_events_depot_access_keys_access_key_id",
                schema: "public",
                table: "session_capture_events",
                column: "access_key_id",
                principalSchema: "public",
                principalTable: "depot_access_keys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_events_workspaces_workspace_id",
                schema: "public",
                table: "session_capture_events",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_inbox_events_session_capture_turn_routes_tu~",
                schema: "public",
                table: "session_capture_inbox_events",
                columns: new[] { "turn_route_id", "access_key_id", "agent", "client_session_id", "turn_id" },
                principalSchema: "public",
                principalTable: "session_capture_turn_routes",
                principalColumns: new[] { "id", "access_key_id", "agent", "client_session_id", "turn_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_turn_routes_depot_access_keys_access_key_id",
                schema: "public",
                table: "session_capture_turn_routes",
                column: "access_key_id",
                principalSchema: "public",
                principalTable: "depot_access_keys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_capture_turn_routes_workspaces_workspace_id",
                schema: "public",
                table: "session_capture_turn_routes",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_workspace_access_grants_access_key",
                schema: "public",
                table: "workspace_access_grants",
                column: "depot_access_key_id",
                principalSchema: "public",
                principalTable: "depot_access_keys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_workspace_access_grants_workspace",
                schema: "public",
                table: "workspace_access_grants",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_workspace_subject_bindings_workspace",
                schema: "public",
                table: "workspace_subject_bindings",
                column: "workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_workspaces_workspaces_parent_workspace_id",
                schema: "public",
                table: "workspaces",
                column: "parent_workspace_id",
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.RenameTable(
                name: "depot_access_keys", schema: "public", newName: "access_keys", newSchema: "public");
            migrationBuilder.RenameColumn(
                name: "depot_access_key_id", schema: "public", table: "workspace_access_grants", newName: "access_key_id");

            migrationBuilder.Sql("""
                ALTER TABLE public.access_keys RENAME CONSTRAINT pk_depot_access_keys TO pk_access_keys;
                ALTER TABLE public.access_keys RENAME CONSTRAINT ck_depot_access_keys_access_version TO ck_access_keys_access_version;
                ALTER INDEX public.ux_depot_access_keys_key_prefix RENAME TO ux_access_keys_key_prefix;
                ALTER INDEX public.ix_depot_access_keys_name RENAME TO ix_access_keys_name;
                ALTER TABLE public.agent_sessions RENAME CONSTRAINT "FK_agent_sessions_depot_access_keys_access_key_id" TO "FK_agent_sessions_access_keys_access_key_id";
                ALTER TABLE public.context_capture_evidence RENAME CONSTRAINT "FK_context_capture_evidence_depot_access_keys_access_key_id" TO "FK_context_capture_evidence_access_keys_access_key_id";
                ALTER TABLE public.session_capture_events RENAME CONSTRAINT "FK_session_capture_events_depot_access_keys_access_key_id" TO "FK_session_capture_events_access_keys_access_key_id";
                ALTER TABLE public.session_capture_turn_routes RENAME CONSTRAINT "FK_session_capture_turn_routes_depot_access_keys_access_key_id" TO "FK_session_capture_turn_routes_access_keys_access_key_id";

                DO $$
                DECLARE collection record;
                BEGIN
                    FOR collection IN
                        SELECT table_schema, table_name
                        FROM information_schema.columns
                        WHERE table_name ~ '^(context|document)_vectors_[a-fA-F0-9]{16}$'
                            AND column_name = 'depot_id'
                    LOOP
                        EXECUTE format('ALTER TABLE %I.%I DROP COLUMN depot_id', collection.table_schema, collection.table_name);
                    END LOOP;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(InfrastructureErrorCodes.SchemaRollbackRequiresBackup);
    }
}
