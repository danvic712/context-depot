using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseManagedConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    normalized_key = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(key)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_settings", x => x.id);
                    table.CheckConstraint("ck_application_settings_value_scalar", "jsonb_typeof(value) IN ('string', 'number', 'boolean')");
                });

            migrationBuilder.CreateTable(
                name: "inference_providers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    protocol_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    protected_api_key = table.Column<string>(type: "text", nullable: true),
                    verification_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inference_providers", x => x.id);
                    table.CheckConstraint("ck_inference_providers_protocol_code", "protocol_code = 'openai-compatible'");
                });

            migrationBuilder.CreateTable(
                name: "inference_routes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dimensions = table.Column<int>(type: "integer", nullable: true),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    embedding_profile_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    index_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    index_generation = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inference_routes", x => x.id);
                    table.CheckConstraint("ck_inference_routes_capability", "capability IN ('chat', 'embedding')");
                    table.CheckConstraint("ck_inference_routes_dimensions", "(capability = 'chat' AND dimensions IS NULL) OR (capability = 'embedding' AND (model_name IS NULL OR dimensions > 0))");
                    table.CheckConstraint("ck_inference_routes_index_generation", "index_generation >= 0");
                    table.CheckConstraint("ck_inference_routes_provider_model_pair", "(provider_id IS NULL AND model_name IS NULL) OR (provider_id IS NOT NULL AND model_name IS NOT NULL)");
                    table.CheckConstraint("ck_inference_routes_timeout_seconds", "timeout_seconds BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "fk_inference_routes_provider",
                        column: x => x.provider_id,
                        principalSchema: "public",
                        principalTable: "inference_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "application_settings",
                columns: new[] { "id", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("01995f60-0000-7000-8000-000000000001"), "ContextDepot:Retrieval:Semantic:ScopeTopK", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "8" },
                    { new Guid("01995f60-0000-7000-8000-000000000002"), "ContextDepot:Retrieval:Semantic:CandidateTopKPerSource", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "20" },
                    { new Guid("01995f60-0000-7000-8000-000000000003"), "ContextDepot:Retrieval:Semantic:OversampleFactor", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "3" },
                    { new Guid("01995f60-0000-7000-8000-000000000004"), "ContextDepot:Retrieval:Semantic:RetrievalLexicalFallbackThreshold", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "0.65" },
                    { new Guid("01995f60-0000-7000-8000-000000000005"), "ContextDepot:Retrieval:Semantic:DedupSimilarityThreshold", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "0.98" },
                    { new Guid("01995f60-0000-7000-8000-000000000006"), "ContextDepot:Retrieval:Semantic:DedupTokenOverlapThreshold", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "0.8" },
                    { new Guid("01995f60-0000-7000-8000-000000000007"), "ContextDepot:Retrieval:Search:DefaultLimit", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "10" },
                    { new Guid("01995f60-0000-7000-8000-000000000008"), "ContextDepot:Retrieval:Search:MaxLimit", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "50" },
                    { new Guid("01995f60-0000-7000-8000-000000000009"), "ContextDepot:IndexRepair:PollIntervalSeconds", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "30" },
                    { new Guid("01995f60-0000-7000-8000-000000000010"), "ContextDepot:IndexRepair:BatchSize", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "32" },
                    { new Guid("01995f60-0000-7000-8000-000000000011"), "ContextDepot:IndexRepair:MaxBatchesPerCycle", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "4" },
                    { new Guid("01995f60-0000-7000-8000-000000000012"), "ContextDepot:VectorCoverage:CacheDurationSeconds", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "30" },
                    { new Guid("01995f60-0000-7000-8000-000000000013"), "ContextDepot:Appearance:Language", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "\"zh-CN\"" },
                    { new Guid("01995f60-0000-7000-8000-000000000014"), "ContextDepot:Appearance:Theme", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "\"system\"" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "inference_routes",
                columns: new[] { "id", "capability", "created_at", "dimensions", "embedding_profile_fingerprint", "index_generation", "index_state", "model_name", "provider_id", "timeout_seconds", "updated_at" },
                values: new object[,]
                {
                    { new Guid("01995f60-0000-7000-8000-000000000101"), "chat", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, 0L, "unconfigured", null, null, 60, new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("01995f60-0000-7000-8000-000000000102"), "embedding", new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, 0L, "unconfigured", null, null, 60, new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "ux_application_settings_normalized_key",
                schema: "public",
                table: "application_settings",
                column: "normalized_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inference_routes_provider_id",
                schema: "public",
                table: "inference_routes",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ux_inference_routes_capability",
                schema: "public",
                table: "inference_routes",
                column: "capability",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inference_routes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inference_providers",
                schema: "public");
        }
    }
}
