using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeConstraintNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.context_items
                SET verification_status = CASE verification_status
                    WHEN 'Unverified' THEN 'Unknown'
                    WHEN 'SelfReported' THEN 'Explicit'
                    ELSE verification_status
                END,
                provenance_trust = CASE provenance_trust
                    WHEN 'AgentReported' THEN 'Asserted'
                    WHEN 'Imported' THEN 'Asserted'
                    WHEN 'Verified' THEN 'Attested'
                    ELSE provenance_trust
                END
                WHERE verification_status IN ('Unverified', 'SelfReported')
                   OR provenance_trust IN ('AgentReported', 'Imported', 'Verified');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_context_items_context_items_supersedes_id_owner_id_workspac~",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropForeignKey(
                name: "FK_context_items_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropForeignKey(
                name: "FK_document_chunks_documents_document_id_owner_id_workspace_id",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_workspaces_owners_owner_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_workspaces_workspaces_parent_workspace_id_owner_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_workspaces_id_owner_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workspaces",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "ux_workspaces_id_owner",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_owners",
                schema: "public",
                table: "owners");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_documents_id_owner_id_workspace_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_documents",
                schema: "public",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ux_documents_id_owner_workspace",
                schema: "public",
                table: "documents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_document_chunks",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_context_items_id_owner_id_workspace_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_context_items",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropIndex(
                name: "ux_context_items_id_owner_workspace",
                schema: "public",
                table: "context_items");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_workspaces_id_owner_id",
                schema: "public",
                table: "workspaces",
                columns: new[] { "id", "owner_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_workspaces",
                schema: "public",
                table: "workspaces",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_owners",
                schema: "public",
                table: "owners",
                column: "id");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_documents_id_owner_id_workspace_id",
                schema: "public",
                table: "documents",
                columns: new[] { "id", "owner_id", "workspace_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_documents",
                schema: "public",
                table: "documents",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_document_chunks",
                schema: "public",
                table: "document_chunks",
                column: "id");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_context_items_id_owner_id_workspace_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "id", "owner_id", "workspace_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_context_items",
                schema: "public",
                table: "context_items",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_context_items_supersedes",
                schema: "public",
                table: "context_items",
                columns: new[] { "supersedes_id", "owner_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "context_items",
                principalColumns: new[] { "id", "owner_id", "workspace_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_context_items_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "workspace_id", "owner_id" },
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumns: new[] { "id", "owner_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_chunks_documents_document_id_owner_id_workspace_id",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "owner_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "documents",
                principalColumns: new[] { "id", "owner_id", "workspace_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "documents",
                columns: new[] { "workspace_id", "owner_id" },
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumns: new[] { "id", "owner_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_workspaces_owners_owner_id",
                schema: "public",
                table: "workspaces",
                column: "owner_id",
                principalSchema: "public",
                principalTable: "owners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_workspaces_workspaces_parent_workspace_id_owner_id",
                schema: "public",
                table: "workspaces",
                columns: new[] { "parent_workspace_id", "owner_id" },
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumns: new[] { "id", "owner_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.context_items
                SET verification_status = CASE verification_status
                    WHEN 'Unknown' THEN 'Unverified'
                    WHEN 'Explicit' THEN 'SelfReported'
                    ELSE verification_status
                END,
                provenance_trust = CASE provenance_trust
                    WHEN 'Asserted' THEN 'AgentReported'
                    WHEN 'Attested' THEN 'Verified'
                    ELSE provenance_trust
                END
                WHERE verification_status IN ('Unknown', 'Explicit')
                   OR provenance_trust IN ('Asserted', 'Attested');
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_context_items_supersedes",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropForeignKey(
                name: "fk_context_items_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropForeignKey(
                name: "fk_document_chunks_documents_document_id_owner_id_workspace_id",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "fk_workspaces_owners_owner_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropForeignKey(
                name: "fk_workspaces_workspaces_parent_workspace_id_owner_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_workspaces_id_owner_id",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "pk_workspaces",
                schema: "public",
                table: "workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "pk_owners",
                schema: "public",
                table: "owners");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_documents_id_owner_id_workspace_id",
                schema: "public",
                table: "documents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_documents",
                schema: "public",
                table: "documents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_document_chunks",
                schema: "public",
                table: "document_chunks");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_context_items_id_owner_id_workspace_id",
                schema: "public",
                table: "context_items");

            migrationBuilder.DropPrimaryKey(
                name: "pk_context_items",
                schema: "public",
                table: "context_items");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_workspaces_id_owner_id",
                schema: "public",
                table: "workspaces",
                columns: new[] { "id", "owner_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_workspaces",
                schema: "public",
                table: "workspaces",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_owners",
                schema: "public",
                table: "owners",
                column: "id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_documents_id_owner_id_workspace_id",
                schema: "public",
                table: "documents",
                columns: new[] { "id", "owner_id", "workspace_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_documents",
                schema: "public",
                table: "documents",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_document_chunks",
                schema: "public",
                table: "document_chunks",
                column: "id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_context_items_id_owner_id_workspace_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "id", "owner_id", "workspace_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_context_items",
                schema: "public",
                table: "context_items",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ux_workspaces_id_owner",
                schema: "public",
                table: "workspaces",
                columns: new[] { "id", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_documents_id_owner_workspace",
                schema: "public",
                table: "documents",
                columns: new[] { "id", "owner_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_context_items_id_owner_workspace",
                schema: "public",
                table: "context_items",
                columns: new[] { "id", "owner_id", "workspace_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_context_items_context_items_supersedes_id_owner_id_workspac~",
                schema: "public",
                table: "context_items",
                columns: new[] { "supersedes_id", "owner_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "context_items",
                principalColumns: new[] { "id", "owner_id", "workspace_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_context_items_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "context_items",
                columns: new[] { "workspace_id", "owner_id" },
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumns: new[] { "id", "owner_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_document_chunks_documents_document_id_owner_id_workspace_id",
                schema: "public",
                table: "document_chunks",
                columns: new[] { "document_id", "owner_id", "workspace_id" },
                principalSchema: "public",
                principalTable: "documents",
                principalColumns: new[] { "id", "owner_id", "workspace_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_workspaces_workspace_id_owner_id",
                schema: "public",
                table: "documents",
                columns: new[] { "workspace_id", "owner_id" },
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumns: new[] { "id", "owner_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workspaces_owners_owner_id",
                schema: "public",
                table: "workspaces",
                column: "owner_id",
                principalSchema: "public",
                principalTable: "owners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workspaces_workspaces_parent_workspace_id_owner_id",
                schema: "public",
                table: "workspaces",
                columns: new[] { "parent_workspace_id", "owner_id" },
                principalSchema: "public",
                principalTable: "workspaces",
                principalColumns: new[] { "id", "owner_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
