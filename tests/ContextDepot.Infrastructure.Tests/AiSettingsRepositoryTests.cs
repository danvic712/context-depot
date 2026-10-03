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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector;
using System.Data.Common;

namespace ContextDepot.Infrastructure.Tests;

public sealed class AiSettingsRepositoryTests
{
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
                o => o.MigrationsHistoryTable("ef_migrations", "public")).AddInterceptors(new IsolatedSchemaInterceptor(schema)));
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
            services.AddScoped<AiSettingsRepository>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector");
            await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<InferenceRuntimeSnapshotRefresher>().RefreshAsync(default);
            var repository = scope.ServiceProvider.GetRequiredService<AiSettingsRepository>();
            var initial = await repository.GetProvidersAsync(default);
            var request = new SaveAiProviderRequest(null, "Shared provider", "https://example.test/v1/", "example-test-key", null,
                new("embedding-model", 3, 30), new("chat-model", null, 60),
                initial.Routes.Single(route => route.Capability == "embedding").UpdatedAt,
                initial.Routes.Single(route => route.Capability == "chat").UpdatedAt);
            var result = await repository.SaveProviderAsync(request, default);
            var shared = Assert.Single(result.Providers);
            Assert.All(result.Routes, route => Assert.Equal(shared.Id, route.ProviderId));
            Assert.Equal("active", result.Routes.Single(route => route.Capability == "embedding").RuntimeState);
            Assert.Equal("chat-model", result.Routes.Single(route => route.Capability == "chat").Model);
            var protectedKey = await db.InferenceProviders.AsNoTracking().Select(item => item.ProtectedApiKey).SingleAsync();
            Assert.NotEqual("example-test-key", protectedKey);
            Assert.DoesNotContain("example-test-key", System.Text.Json.JsonSerializer.Serialize(result));
            db.ChangeTracker.Clear();
            var update = request with { Id = shared.Id, UpdatedAt = shared.UpdatedAt, ApiKey = null,
                EmbeddingUpdatedAt = result.Routes.Single(route => route.Capability == "embedding").UpdatedAt,
                ChatUpdatedAt = result.Routes.Single(route => route.Capability == "chat").UpdatedAt,
                Chat = new("chat-model-v2", null, 90) };
            var saved = await repository.SaveProviderAsync(update, default);
            Assert.Single(saved.Providers);
            Assert.Equal(protectedKey, await db.InferenceProviders.AsNoTracking().Select(item => item.ProtectedApiKey).SingleAsync());
            db.ChangeTracker.Clear();
            var conflict = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.SaveProviderAsync(update with
                { Name = "Should not persist", Chat = new("should-not-persist", null, 30) }, default));
            Assert.Equal(ApplicationErrorCodes.SettingsConflict, conflict.ErrorCode);
            db.ChangeTracker.Clear();
            var unchanged = await repository.GetProvidersAsync(default);
            Assert.Equal("Shared provider", Assert.Single(unchanged.Providers).Name);
            Assert.Equal("chat-model-v2", unchanged.Routes.Single(route => route.Capability == "chat").Model);
            var disabled = await repository.SaveProviderAsync(update with { UpdatedAt = unchanged.Providers[0].UpdatedAt,
                EmbeddingUpdatedAt = unchanged.Routes.Single(route => route.Capability == "embedding").UpdatedAt,
                ChatUpdatedAt = unchanged.Routes.Single(route => route.Capability == "chat").UpdatedAt, Chat = null }, default);
            Assert.Null(disabled.Routes.Single(route => route.Capability == "chat").ProviderId);
            Assert.Equal(shared.Id, disabled.Routes.Single(route => route.Capability == "embedding").ProviderId);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", database);
            await drop.ExecuteNonQueryAsync();
        }
    }

    // Production SQL uses public explicitly. Keep both migrations and row-lock queries inside this test's schema.
    private sealed class IsolatedSchemaInterceptor(string schema) : DbCommandInterceptor
    {
        private void Rewrite(DbCommand command) => command.CommandText = command.CommandText
            .Replace("\"public\"", $"\"{schema}\"", StringComparison.Ordinal)
            .Replace("'public'", $"'{schema}'", StringComparison.Ordinal)
            .Replace("public.", schema + ".", StringComparison.Ordinal);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Rewrite(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Rewrite(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Rewrite(command); return ValueTask.FromResult(result); }
    }
}
