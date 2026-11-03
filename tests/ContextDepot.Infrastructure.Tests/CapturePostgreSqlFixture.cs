using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.AgentSessions;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ContextDepot.Infrastructure.Tests;

public sealed class CapturePostgreSqlFactAttribute : FactAttribute
{
    public CapturePostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CapturePostgreSqlFixture.ConnectionVariable)))
            Skip = $"Real PostgreSQL requires {CapturePostgreSqlFixture.ConnectionVariable}; skipped is not acceptance.";
    }
}

[CollectionDefinition(Name)]
public sealed class CapturePostgreSqlCollection : ICollectionFixture<CapturePostgreSqlFixture>
{
    public const string Name = "Capture real PostgreSQL";
}

public sealed class CapturePostgreSqlFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "CONTEXTDEPOT_CAPTURE_TEST_CONNECTION";
    private string? connectionString;
    public SessionContentProtector Protector { get; } = new(new EphemeralDataProtectionProvider());
    public CaptureEvidenceProtector EvidenceProtector { get; } = new(new EphemeralDataProtectionProvider());

    public async Task InitializeAsync()
    {
        connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database?.StartsWith("contextdepot_capture_test_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Capture tests refuse databases without the contextdepot_capture_test_ prefix.");
        // No SQL rewriting: migrations and raw repository SQL both target the real public schema.
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask; // Never drop a database or schema.

    public ContextDepotDbContext CreateDb(params IInterceptor[] interceptors)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Capture test connection is unset.");
        var access = new CurrentDepotAccessContext();
        access.AllowInternalAccess();
        return new(new DbContextOptionsBuilder<ContextDepotDbContext>().UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("ef_migrations", "public"))
            .AddInterceptors(interceptors).Options, access);
    }

    public async Task<CaptureSeed> SeedAsync()
    {
        await using var db = CreateDb();
        // Explicit child-first list, confined to the dedicated test database. No CASCADE or schema deletion.
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM public.context_capture_evidence;
            DELETE FROM public.session_capture_inbox_events;
            DELETE FROM public.session_capture_turn_routes;
            DELETE FROM public.session_capture_events;
            DELETE FROM public.context_items;
            DELETE FROM public.workspace_access_grants;
            DELETE FROM public.depot_access_keys;
            DELETE FROM public.workspaces;
            DELETE FROM public.depots;
            """);
        var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.NewGuid(), "Capture integration", now);
        var workspace = new Workspace(Guid.NewGuid(), depot.Id, null, "Storage", "storage", null, now);
        var key = new DepotAccessKey(Guid.NewGuid(), depot.Id, "Capture", "capture-test", "test-only-hash", now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, workspace.Id, now));
        db.AddRange(depot, workspace, key);
        await db.SaveChangesAsync();
        return new(depot.Id, key.Id, workspace.Id);
    }

    public SessionCaptureInboxRepository Inbox(ContextDepotDbContext db, int capacity = 200) => new(db, Protector, Options(capacity: capacity));
    public static IOptionsMonitor<SessionCaptureOptions> Options(int calls = 30, int capacity = 200) =>
        new CaptureOptionsMonitor(new() { Enabled = true, MaxCallsPerHour = calls, MaxPendingEventsPerKey = capacity });

    public SessionCaptureInboxEvent Message(CaptureSeed seed, string phase, string text = "I use PostgreSQL for persistent storage.", string turn = "turn-1") => new()
    {
        Id = Guid.NewGuid(), DepotId = seed.DepotId, AccessKeyId = seed.KeyId, Agent = "codex",
        ClientSessionId = "capture-session", TurnId = turn, EventId = turn + ":" + phase, Phase = phase,
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
        EncryptedContent = Protector.Protect(text), CreatedAt = DateTimeOffset.UtcNow
    };
    public static AuthorizedCaptureEnvelope Envelope(params SessionCaptureInboxEvent[] messages) => new(messages, false, DateTimeOffset.UtcNow.AddHours(24));
    public async Task<RouteLocator> AcceptPairAsync(CaptureSeed seed)
    {
        await using var db = CreateDb();
        var receipt = await Inbox(db).AcceptAsync(Envelope(Message(seed, "user"), Message(seed, "assistant", "Understood.")), default);
        return new(seed.DepotId, seed.KeyId, receipt.TurnReceiptId!.Value);
    }
    public async Task ResolveAsync(RouteLocator locator, Guid workspaceId)
    {
        await using var db = CreateDb();
        var inbox = Inbox(db);
        var lease = Assert.IsType<RouteLease>(await inbox.TryClaimRouteAsync(locator, default));
        Assert.True(await inbox.CompleteRouteAsync(lease, new("resolved", workspaceId), default));
    }
}

public sealed record CaptureSeed(Guid DepotId, Guid KeyId, Guid WorkspaceId);
internal sealed class CaptureOptionsMonitor(SessionCaptureOptions value) : IOptionsMonitor<SessionCaptureOptions>
{
    public SessionCaptureOptions CurrentValue => value;
    public SessionCaptureOptions Get(string? name) => value;
    public IDisposable? OnChange(Action<SessionCaptureOptions, string?> listener) => null;
}

// Throws after EF has sent the statements, before the repository commits its transaction.
internal sealed class CaptureSaveFault(Func<ContextDepotDbContext, bool> shouldFail) : SaveChangesInterceptor
{
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ContextDepotDbContext db && shouldFail(db))
            throw new InvalidOperationException("capture-test-injected-save-failure");
        return ValueTask.FromResult(result);
    }
}
