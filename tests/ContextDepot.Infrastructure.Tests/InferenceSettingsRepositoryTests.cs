using ContextDepot.Application.DataProtection;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.DataProtection;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ContextDepot.Domain.Inferences;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector;

namespace ContextDepot.Infrastructure.Tests;

public sealed class InferenceSettingsRepositoryTests
{
    [PostgreSqlFact]
    public async Task SeedMigrationPreservesExistingProviderCredentialsAndAssignedRoute()
    {
        var connection = Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")!;
        var schema = "codex_inference_seed_" + Guid.CreateVersion7().ToString("N");
        await using var database = new NpgsqlConnection(connection);
        await database.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", database))
            await create.ExecuteNonQueryAsync();
        try
        {
            var access = new CurrentDepotAccessContext(); access.AllowInternalAccess();
            var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
                .UseNpgsql(connection, o => o.MigrationsHistoryTable("ef_migrations", "public"))
                .AddInterceptors(new InferenceTestSchemaInterceptor(schema)).Options;
            await using var db = new ContextDepotDbContext(options, access);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260922232251_AddDatabaseManagedConfiguration");
            var id = Guid.CreateVersion7(); var revision = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public.inference_providers
                (id, name, protocol_code, base_url, protected_api_key, verification_state, created_at, updated_at)
                VALUES ({id}, 'OpenAI', 'openai-compatible', 'https://existing.example/v1/', 'existing-encrypted-key', 'unverified', {revision}, {revision});
                UPDATE public.inference_routes SET provider_id = {id}, model_name = 'existing-chat', updated_at = {revision}
                WHERE capability = 'chat';
                """);
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            var providers = await db.InferenceProviders.AsNoTracking().ToArrayAsync();
            Assert.Equal(3, providers.Length);
            var existing = Assert.Single(providers, item => item.Name == "OpenAI");
            Assert.Equal(id, existing.Id);
            Assert.Equal(InferenceProviderKinds.Custom, existing.Kind);
            Assert.Equal("https://existing.example/v1/", existing.BaseUrl);
            Assert.Equal("existing-encrypted-key", existing.ProtectedApiKey);
            Assert.Equal(revision.ToUnixTimeMilliseconds(), existing.UpdatedAt.ToUnixTimeMilliseconds());
            Assert.All(providers.Where(item => item.Id != id), item => Assert.Null(item.ProtectedApiKey));
            var route = await db.InferenceRoutes.AsNoTracking().SingleAsync(item => item.Capability == "chat");
            Assert.Equal(id, route.ProviderId);
            Assert.Equal("existing-chat", route.ModelName);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", database);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task ProviderSharesTwoModelsPreservesOneEncryptedKeyAndRejectsStaleAtomicChanges()
    {
        var connection = Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")!;
        var schema = "codex_provider_test_" + Guid.CreateVersion7().ToString("N");
        await using var database = new NpgsqlConnection(connection);
        await database.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", database))
            await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connection);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<IWorkspaceAccessContext>(_ => { var access = new CurrentDepotAccessContext(); access.AllowInternalAccess(); return access; });
            services.AddDbContext<ContextDepotDbContext>(options => options.UseNpgsql(builder.ConnectionString,
                o => o.MigrationsHistoryTable("ef_migrations", "public")).AddInterceptors(new InferenceTestSchemaInterceptor(schema)));
            services.AddSingleton<ISecretProtector>(new DataProtectionSecretProtector(new EphemeralDataProtectionProvider()));
            services.AddSingleton<IIdGenerator, GuidV7IdGenerator>();
            services.AddSingleton(TimeProvider.System);
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.ConnectionString); dataSourceBuilder.UseVector();
            services.AddSingleton(dataSourceBuilder.Build());
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new PostgreSqlVectorStoreOptions { Schema = schema }));
            services.AddSingleton<PostgreSqlVectorStore>();
            services.AddSingleton<VectorCollectionInitializer>();
            services.AddSingleton<InferenceRuntimeSnapshotAccessor>();
            services.AddSingleton<InferenceRuntimeSnapshotLoader>();
            services.AddSingleton<InferenceRuntimeSnapshotRefresher>();
            services.AddScoped<InferenceSettingsRepository>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector");
            await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<InferenceRuntimeSnapshotRefresher>().RefreshAsync(default);
            var repository = scope.ServiceProvider.GetRequiredService<InferenceSettingsRepository>();
            var initial = await repository.GetProvidersAsync(default);
            Assert.Equal(new[] { "Azure OpenAI", "DeepSeek", "OpenAI" }, initial.Providers.Select(item => item.Name));
            Assert.All(initial.Providers, item => Assert.False(item.HasApiKey));
            Assert.All(initial.Routes, item => Assert.Null(item.ProviderId));
            Assert.Null(initial.Providers.Single(item => item.Kind == "azure-openai").Endpoint);
            await db.Database.MigrateAsync();
            Assert.Equal(3, await db.InferenceProviders.CountAsync());
            var request = new SaveInferenceProviderRequest(null, "Shared provider", "https://example.test/v1/", "example-test-key", null,
                new("embedding-model", 3, 30), new("chat-model", null, 60),
                initial.Routes.Single(route => route.Capability == "embedding").UpdatedAt,
                initial.Routes.Single(route => route.Capability == "chat").UpdatedAt);
            var result = await repository.SaveProviderAsync(request, default);
            var shared = Assert.Single(result.Providers, item => item.Name == "Shared provider");
            Assert.Equal(4, result.Providers.Count);
            Assert.All(result.Routes, route => Assert.Equal(shared.Id, route.ProviderId));
            Assert.Equal("active", result.Routes.Single(route => route.Capability == "embedding").RuntimeState);
            Assert.Equal("chat-model", result.Routes.Single(route => route.Capability == "chat").Model);
            var protectedKey = await db.InferenceProviders.AsNoTracking().Where(item => item.Id == shared.Id).Select(item => item.ProtectedApiKey).SingleAsync();
            Assert.NotEqual("example-test-key", protectedKey);
            Assert.DoesNotContain("example-test-key", System.Text.Json.JsonSerializer.Serialize(result));
            db.ChangeTracker.Clear();
            var update = request with { Id = shared.Id, UpdatedAt = shared.UpdatedAt, ApiKey = null,
                EmbeddingUpdatedAt = result.Routes.Single(route => route.Capability == "embedding").UpdatedAt,
                ChatUpdatedAt = result.Routes.Single(route => route.Capability == "chat").UpdatedAt,
                Chat = new("chat-model-v2", null, 90) };
            var saved = await repository.SaveProviderAsync(update, default);
            Assert.Equal(4, saved.Providers.Count);
            Assert.Equal(protectedKey, await db.InferenceProviders.AsNoTracking().Where(item => item.Id == shared.Id).Select(item => item.ProtectedApiKey).SingleAsync());
            db.ChangeTracker.Clear();
            var conflict = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.SaveProviderAsync(update with
                { Name = "Should not persist", Chat = new("should-not-persist", null, 30) }, default));
            Assert.Equal(ApplicationErrorCodes.SettingsConflict, conflict.ErrorCode);
            db.ChangeTracker.Clear();
            var unchanged = await repository.GetProvidersAsync(default);
            Assert.Equal("Shared provider", Assert.Single(unchanged.Providers, item => item.Id == shared.Id).Name);
            Assert.Equal("chat-model-v2", unchanged.Routes.Single(route => route.Capability == "chat").Model);
            var disabled = await repository.SaveProviderAsync(update with { UpdatedAt = unchanged.Providers.Single(item => item.Id == shared.Id).UpdatedAt,
                EmbeddingUpdatedAt = unchanged.Routes.Single(route => route.Capability == "embedding").UpdatedAt,
                ChatUpdatedAt = unchanged.Routes.Single(route => route.Capability == "chat").UpdatedAt, Chat = null }, default);
            Assert.Null(disabled.Routes.Single(route => route.Capability == "chat").ProviderId);
            Assert.Equal(shared.Id, disabled.Routes.Single(route => route.Capability == "embedding").ProviderId);
            db.ChangeTracker.Clear();
            var deepSeek = disabled.Providers.Single(item => item.Kind == "deepseek");
            var split = await repository.SaveProviderAsync(request with {
                Id = deepSeek.Id, Name = deepSeek.Name, Kind = deepSeek.Kind, UpdatedAt = deepSeek.UpdatedAt,
                Endpoint = deepSeek.Endpoint!, ApiKey = "deepseek-example-key", Embedding = null, Chat = new("deepseek-chat-test", null, 30),
                EmbeddingUpdatedAt = disabled.Routes.Single(item => item.Capability == "embedding").UpdatedAt,
                ChatUpdatedAt = disabled.Routes.Single(item => item.Capability == "chat").UpdatedAt
            }, default);
            var originalEmbedding = split.Routes.Single(item => item.Capability == "embedding");
            var originalProfile = await db.InferenceRoutes.AsNoTracking().Where(item => item.Capability == "embedding")
                .Select(item => new { item.IndexGeneration, item.EmbeddingProfileFingerprint }).SingleAsync();
            var selectedChat = split.Routes.Single(item => item.Capability == "chat");
            Assert.Equal(shared.Id, originalEmbedding.ProviderId);
            Assert.Equal(deepSeek.Id, selectedChat.ProviderId);
            Assert.Equal("active", originalEmbedding.RuntimeState);
            var savedShared = split.Providers.Single(item => item.Id == shared.Id);
            var savedDeepSeek = split.Providers.Single(item => item.Id == deepSeek.Id);
            db.ChangeTracker.Clear();
            var switched = await repository.SaveAsync("chat", new(savedShared.Id, "other-chat", null, 45,
                selectedChat.UpdatedAt, savedShared.UpdatedAt), default);
            Assert.Equal(savedShared.Id, switched.ProviderId);
            Assert.Equal(originalEmbedding, (await repository.GetAsync(default)).Single(item => item.Capability == "embedding"));
            Assert.Equal(savedShared.UpdatedAt, (await repository.GetProvidersAsync(default)).Providers.Single(item => item.Id == shared.Id).UpdatedAt);
            Assert.Equal(protectedKey, await db.InferenceProviders.AsNoTracking().Where(item => item.Id == shared.Id).Select(item => item.ProtectedApiKey).SingleAsync());
            db.ChangeTracker.Clear();
            var separated = await repository.SaveAsync("chat", new(savedDeepSeek.Id, "deepseek-chat-v2", null, 60,
                switched.UpdatedAt, savedDeepSeek.UpdatedAt), default);
            Assert.Equal(savedDeepSeek.Id, separated.ProviderId);
            Assert.Equal(originalEmbedding, (await repository.GetAsync(default)).Single(item => item.Capability == "embedding"));
            db.ChangeTracker.Clear();
            var staleProvider = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.SaveAsync("chat",
                new(savedShared.Id, "must-not-persist", null, 30, separated.UpdatedAt, savedShared.UpdatedAt.AddSeconds(-1)), default));
            Assert.Equal(ApplicationErrorCodes.SettingsConflict, staleProvider.ErrorCode);
            db.ChangeTracker.Clear();
            var unsupported = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.SaveAsync("embedding",
                new(savedDeepSeek.Id, "unsupported-embedding", 3, 30, originalEmbedding.UpdatedAt, savedDeepSeek.UpdatedAt), default));
            Assert.Equal(ApplicationErrorCodes.InvalidInferenceConfiguration, unsupported.ErrorCode);
            db.ChangeTracker.Clear();
            var cleared = await repository.SaveAsync("chat", new(null, null, null, 60, separated.UpdatedAt, null), default);
            Assert.Null(cleared.ProviderId);
            Assert.Equal(originalEmbedding, (await repository.GetAsync(default)).Single(item => item.Capability == "embedding"));
            var preservedProfile = await db.InferenceRoutes.AsNoTracking().Where(item => item.Capability == "embedding")
                .Select(item => new { item.IndexGeneration, item.EmbeddingProfileFingerprint }).SingleAsync();
            Assert.Equal(originalProfile, preservedProfile);
            db.ChangeTracker.Clear();
            var changedEmbedding = await repository.SaveAsync("embedding", new(savedShared.Id, "new-embedding-model", 6, 45,
                originalEmbedding.UpdatedAt, savedShared.UpdatedAt), default);
            Assert.Equal("active", changedEmbedding.RuntimeState);
            Assert.Equal("new-embedding-model", changedEmbedding.Model);
            Assert.Equal(6, changedEmbedding.Dimensions);
            Assert.Equal(cleared, (await repository.GetAsync(default)).Single(item => item.Capability == "chat"));
            var changedProfile = await db.InferenceRoutes.AsNoTracking().SingleAsync(item => item.Capability == "embedding");
            Assert.Equal(originalProfile.IndexGeneration + 1, changedProfile.IndexGeneration);
            Assert.NotEqual(originalProfile.EmbeddingProfileFingerprint, changedProfile.EmbeddingProfileFingerprint);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", database);
            await drop.ExecuteNonQueryAsync();
        }
    }

}
