using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDepotModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            // migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "depots",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_depots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "depot_access_keys",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_depot_access_keys", x => x.id);
                    table.UniqueConstraint("ak_depot_access_keys_id_depot_id", x => new { x.id, x.depot_id });
                    table.ForeignKey(
                        name: "fk_depot_access_keys_depot_id",
                        column: x => x.depot_id,
                        principalSchema: "public",
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                    table.UniqueConstraint("ak_workspaces_id_depot_id", x => new { x.id, x.depot_id });
                    table.ForeignKey(
                        name: "fk_workspaces_depots_depot_id",
                        column: x => x.depot_id,
                        principalSchema: "public",
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workspaces_workspaces_parent_workspace_id_depot_id",
                        columns: x => new { x.parent_workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: false),
                    importance = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)50),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    verification_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provenance_trust = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_agent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sensitivity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_context_items", x => x.id);
                    table.UniqueConstraint("ak_context_items_id_depot_id_workspace_id", x => new { x.id, x.depot_id, x.workspace_id });
                    table.CheckConstraint("ck_context_items_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("ck_context_items_importance", "importance BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_context_items_supersedes",
                        columns: x => new { x.supersedes_id, x.depot_id, x.workspace_id },
                        principalSchema: "public",
                        principalTable: "context_items",
                        principalColumns: new[] { "id", "depot_id", "workspace_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_context_items_workspaces_workspace_id_depot_id",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    indexed_content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    index_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    last_index_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.UniqueConstraint("ak_documents_id_depot_id_workspace_id", x => new { x.id, x.depot_id, x.workspace_id });
                    table.ForeignKey(
                        name: "fk_documents_workspaces_workspace_id_depot_id",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspace_access_grants",
                schema: "public",
                columns: table => new
                {
                    depot_access_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_access_grants", x => new { x.depot_access_key_id, x.workspace_id });
                    table.ForeignKey(
                        name: "fk_workspace_access_grants_access_key",
                        columns: x => new { x.depot_access_key_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "depot_access_keys",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workspace_access_grants_workspace",
                        columns: x => new { x.workspace_id, x.depot_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "depot_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    heading_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_chunks_documents_document_id_depot_id_workspace_id",
                        columns: x => new { x.document_id, x.depot_id, x.workspace_id },
                        principalSchema: "public",
                        principalTable: "documents",
                        principalColumns: new[] { "id", "depot_id", "workspace_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_context_items_supersedes_id_depot_id_workspace_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "supersedes_id", "depot_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "IX_context_items_workspace_id_depot_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_context_items_active_key",
                schema: "public",
                table: "context_items",
                columns: new[] { "depot_id", "workspace_id", "key" },
                unique: true,
                filter: "status = 'Active' AND key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_depot_access_keys_depot_name",
                schema: "public",
                table: "depot_access_keys",
                columns: new[] { "depot_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ux_depot_access_keys_key_prefix",
                schema: "public",
                table: "depot_access_keys",
                column: "key_prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_document_id_depot_id_workspace_id",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "depot_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "ux_document_chunks_document_ordinal",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_document_chunks_id_depot_workspace",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "id", "depot_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_workspace_id_depot_id",
                schema: "public",
                table: "documents",
                columns: new[] { "workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_documents_depot_workspace_path",
                schema: "public",
                table: "documents",
                columns: new[] { "depot_id", "workspace_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_access_grants_access_key_depot",
                schema: "public",
                table: "workspace_access_grants",
                columns: new[] { "depot_access_key_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workspace_access_grants_workspace_depot",
                schema: "public",
                table: "workspace_access_grants",
                columns: new[] { "workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_parent_workspace_id_depot_id",
                schema: "public",
                table: "workspaces",
                columns: new[] { "parent_workspace_id", "depot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_child_slug",
                schema: "public",
                table: "workspaces",
                columns: new[] { "depot_id", "parent_workspace_id", "slug" },
                unique: true,
                filter: "parent_workspace_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_root_slug",
                schema: "public",
                table: "workspaces",
                columns: new[] { "depot_id", "slug" },
                unique: true,
                filter: "parent_workspace_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "context_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "document_chunks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "workspace_access_grants",
                schema: "public");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "depot_access_keys",
                schema: "public");

            migrationBuilder.DropTable(
                name: "workspaces",
                schema: "public");

            migrationBuilder.DropTable(
                name: "depots",
                schema: "public");
        }
    }
}
