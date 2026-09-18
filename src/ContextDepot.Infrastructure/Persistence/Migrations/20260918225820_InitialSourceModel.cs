using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSourceModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "owners",
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
                    table.PrimaryKey("PK_owners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.UniqueConstraint("AK_workspaces_id_owner_id", x => new { x.id, x.owner_id });
                    table.ForeignKey(
                        name: "FK_workspaces_owners_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "public",
                        principalTable: "owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspaces_workspaces_parent_workspace_id_owner_id",
                        columns: x => new { x.parent_workspace_id, x.owner_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "owner_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_context_items", x => x.id);
                    table.UniqueConstraint("AK_context_items_id_owner_id_workspace_id", x => new { x.id, x.owner_id, x.workspace_id });
                    table.CheckConstraint("ck_context_items_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("ck_context_items_importance", "importance BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_context_items_context_items_supersedes_id_owner_id_workspac~",
                        columns: x => new { x.supersedes_id, x.owner_id, x.workspace_id },
                        principalSchema: "public",
                        principalTable: "context_items",
                        principalColumns: new[] { "id", "owner_id", "workspace_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_context_items_workspaces_workspace_id_owner_id",
                        columns: x => new { x.workspace_id, x.owner_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "owner_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.UniqueConstraint("AK_documents_id_owner_id_workspace_id", x => new { x.id, x.owner_id, x.workspace_id });
                    table.ForeignKey(
                        name: "FK_documents_workspaces_workspace_id_owner_id",
                        columns: x => new { x.workspace_id, x.owner_id },
                        principalSchema: "public",
                        principalTable: "workspaces",
                        principalColumns: new[] { "id", "owner_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_document_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_chunks_documents_document_id_owner_id_workspace_id",
                        columns: x => new { x.document_id, x.owner_id, x.workspace_id },
                        principalSchema: "public",
                        principalTable: "documents",
                        principalColumns: new[] { "id", "owner_id", "workspace_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_context_items_owner_workspace_key",
                schema: "public",
                table: "context_items",
                columns: new[] { "owner_id", "workspace_id", "key" });

            migrationBuilder.CreateIndex(
                name: "IX_context_items_supersedes_id_owner_id_workspace_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "supersedes_id", "owner_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "IX_context_items_workspace_id_owner_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "workspace_id", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_context_items_id_owner_workspace",
                schema: "public",
                table: "context_items",
                columns: new[] { "id", "owner_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_document_id_owner_id_workspace_id",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "owner_id", "workspace_id" });

            migrationBuilder.CreateIndex(
                name: "ux_document_chunks_document_ordinal",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_document_chunks_id_owner_workspace",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "id", "owner_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_workspace_id_owner_id",
                schema: "public",
                table: "documents",
                columns: new[] { "workspace_id", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_documents_id_owner_workspace",
                schema: "public",
                table: "documents",
                columns: new[] { "id", "owner_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_documents_owner_workspace_path",
                schema: "public",
                table: "documents",
                columns: new[] { "owner_id", "workspace_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_parent_workspace_id_owner_id",
                schema: "public",
                table: "workspaces",
                columns: new[] { "parent_workspace_id", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_child_slug",
                schema: "public",
                table: "workspaces",
                columns: new[] { "owner_id", "parent_workspace_id", "slug" },
                unique: true,
                filter: "parent_workspace_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_id_owner",
                schema: "public",
                table: "workspaces",
                columns: new[] { "id", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_root_slug",
                schema: "public",
                table: "workspaces",
                columns: new[] { "owner_id", "slug" },
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
                name: "documents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "workspaces",
                schema: "public");

            migrationBuilder.DropTable(
                name: "owners",
                schema: "public");
        }
    }
}
