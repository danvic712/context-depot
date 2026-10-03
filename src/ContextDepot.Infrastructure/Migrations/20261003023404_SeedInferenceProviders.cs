using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContextDepot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedInferenceProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "public",
                table: "inference_providers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "custom");

            // Keep existing providers and routes intact; seed only missing names and no credentials.
            migrationBuilder.Sql("""
                INSERT INTO public.inference_providers
                    (id, name, kind, protocol_code, base_url, protected_api_key, verification_state, created_at, updated_at)
                SELECT preset.id, preset.name, preset.kind, 'openai-compatible', preset.base_url, NULL,
                    'unverified', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('d5d613cb-41b5-4ab8-90f3-04b5b986bc01'::uuid, 'OpenAI', 'openai', 'https://api.openai.com/v1/'),
                    ('d5d613cb-41b5-4ab8-90f3-04b5b986bc02'::uuid, 'Azure OpenAI', 'azure-openai', ''),
                    ('d5d613cb-41b5-4ab8-90f3-04b5b986bc03'::uuid, 'DeepSeek', 'deepseek', 'https://api.deepseek.com/v1/')
                ) AS preset(id, name, kind, base_url)
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.inference_providers existing
                    WHERE lower(btrim(existing.name)) = lower(preset.name)
                )
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Configured or assigned providers are user data and survive rollback.
            migrationBuilder.Sql("""
                DELETE FROM public.inference_providers provider
                WHERE provider.id IN (
                    'd5d613cb-41b5-4ab8-90f3-04b5b986bc01'::uuid,
                    'd5d613cb-41b5-4ab8-90f3-04b5b986bc02'::uuid,
                    'd5d613cb-41b5-4ab8-90f3-04b5b986bc03'::uuid)
                    AND provider.protected_api_key IS NULL
                    AND NOT EXISTS (SELECT 1 FROM public.inference_routes route WHERE route.provider_id = provider.id);
                """);
            migrationBuilder.DropColumn(
                name: "kind",
                schema: "public",
                table: "inference_providers");
        }
    }
}
