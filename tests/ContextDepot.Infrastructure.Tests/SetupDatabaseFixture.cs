using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ContextDepot.Infrastructure.Tests;

internal sealed class SetupDatabaseFixture : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly DbContextOptions<ContextDepotDbContext> options;
    private readonly string schema;

    private SetupDatabaseFixture(NpgsqlConnection connection, string schema, DbContextOptions<ContextDepotDbContext> options)
    {
        this.connection = connection;
        this.schema = schema;
        this.options = options;
    }

    public static async Task<SetupDatabaseFixture> CreateAsync(string? migration = null)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")!;
        var schema = "codex_setup_" + Guid.CreateVersion7().ToString("N");
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection))
            await create.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public"))
            .AddInterceptors(new InferenceTestSchemaInterceptor(schema)).Options;
        var fixture = new SetupDatabaseFixture(connection, schema, options);
        try
        {
            await using var db = fixture.CreateDb();
            await db.GetService<IMigrator>().MigrateAsync(migration);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public ContextDepotDbContext CreateDb(bool unrestricted = false)
    {
        var access = new CurrentDepotAccessContext();
        if (unrestricted) access.AllowInternalAccess();
        return new ContextDepotDbContext(options, access);
    }

    public ContextDepotDbContext CreateDb(CurrentDepotAccessContext access) => new(options, access);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", connection);
            await drop.ExecuteNonQueryAsync();
        }
        finally { await connection.DisposeAsync(); }
    }
}
