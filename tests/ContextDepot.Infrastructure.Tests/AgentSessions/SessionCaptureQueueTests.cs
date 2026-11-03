using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.AgentSessions;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Tests.AgentSessions;

public sealed class SessionCaptureQueueTests
{
    [PostgreSqlFact]
    public async Task Provider_budget_is_shared_with_routes_and_wait_does_not_increment_extraction_attempts()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var options = new Monitor { CurrentValue = new() { Enabled = true, MaxCallsPerHour = 1 } };
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var queue = new SessionCaptureRepository(db, protector, options, TimeProvider.System);
        var user = Message(depot, workspace, key, "turn", "user", protector);
        var assistant = Message(depot, workspace, key, "turn", "assistant", protector);
        await queue.EnqueueTurnAsync(user, assistant, default);
        var turn = Assert.IsType<CapturedTurn>(await queue.ClaimAsync(default));
        Assert.Equal(0, user.AttemptCount);
        Assert.Equal(user.LeaseToken, assistant.LeaseToken);
        var budget = new SessionCaptureCallBudget(db, options);
        var extractionLease = new CaptureWorkLease(depot.Id, key.Id, "extraction", user.Id, turn.LeaseToken!.Value);
        Assert.Equal("granted", (await budget.TryReserveAsync(extractionLease, ProviderCallKind.ExtractionChat, default)).Status);
        Assert.Equal(1, user.ProviderCallCount);
        Assert.Equal(1, user.AttemptCount);
        Assert.Equal(0, assistant.ProviderCallCount);
        Assert.Equal(0, assistant.AttemptCount);
        var route = Route(depot, key, "other");
        route.State = "resolving"; route.LeaseToken = Guid.NewGuid(); route.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(1);
        db.SessionCaptureTurnRoutes.Add(route); await db.SaveChangesAsync();
        var waiting = await budget.TryReserveAsync(new(depot.Id, key.Id, "route", route.Id, route.LeaseToken.Value), ProviderCallKind.RoutingChat, default);
        Assert.Equal("wait", waiting.Status);
        Assert.True(waiting.WaitUntil > DateTimeOffset.UtcNow.AddMinutes(59));
        Assert.Equal(0, route.ProviderCallCount);
        var extractionWait = await budget.TryReserveAsync(extractionLease, ProviderCallKind.ExtractionChat, default);
        Assert.Equal("wait", extractionWait.Status);
        Assert.Equal(1, user.AttemptCount);
        Assert.Equal("lease_lost", (await budget.TryReserveAsync(extractionLease with { LeaseToken = Guid.NewGuid() }, ProviderCallKind.ExtractionChat, default)).Status);
        options.CurrentValue = new() { Enabled = false };
        Assert.Equal("lease_lost", (await budget.TryReserveAsync(extractionLease, ProviderCallKind.ExtractionChat, default)).Status);
        options.CurrentValue = new() { Enabled = true, MaxCallsPerHour = 1 };
        // The result must carry the boundary across contexts, rather than relying on a tracked value.
        user.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await queue.FinishAsync(turn, new(SessionCaptureDisposition.Retry, null, "budget_wait", extractionWait.WaitUntil), default);
        db.ChangeTracker.Clear();
        var finished = await db.SessionCaptureEvents.OrderBy(e => e.Phase).ToArrayAsync();
        Assert.All(finished, e =>
        {
            Assert.Equal("pending", e.Status);
            Assert.Equal(extractionWait.WaitUntil, e.NextAttemptAt);
            Assert.Null(e.LeaseToken);
            Assert.Equal(e.Phase == "user" ? 1 : 0, e.AttemptCount);
            Assert.Equal(e.Phase == "user" ? 1 : 0, e.ProviderCallCount);
        });
    }

    [PostgreSqlFact]
    public async Task Budget_rechecks_lease_after_waiting_for_authorization_lock()
    {
        await AssertAuthorizationWaitAsync("route", "lease");
        await AssertAuthorizationWaitAsync("extraction", "lease");
    }

    [PostgreSqlFact]
    public async Task Budget_rechecks_capture_deadline_after_waiting_for_authorization_lock()
    {
        await AssertAuthorizationWaitAsync("route", "deadline");
        await AssertAuthorizationWaitAsync("extraction", "deadline");
    }

    [PostgreSqlFact]
    public async Task Budget_rechecks_enabled_after_waiting_for_authorization_lock()
    {
        await AssertAuthorizationWaitAsync("route", "disabled");
        await AssertAuthorizationWaitAsync("extraction", "disabled");
    }

    private static async Task AssertAuthorizationWaitAsync(string ownerKind, string boundary)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var token = timeout.Token;
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var options = new Monitor();
        var leaseToken = Guid.NewGuid();
        var now = await db.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(token);
        var boundaryAt = now.AddSeconds(2);
        var leaseUntil = boundary == "lease" ? boundaryAt : now.AddMinutes(1);
        var expiresAt = boundary == "deadline" ? boundaryAt : now.AddHours(24);
        var route = Route(depot, key, "blocked");
        var user = Message(depot, workspace, key, "blocked", "user", protector);
        if (ownerKind == "route")
        {
            route.State = "resolving"; route.LeaseToken = leaseToken;
            route.LeaseUntil = leaseUntil; route.ExpiresAt = expiresAt;
            db.SessionCaptureTurnRoutes.Add(route);
        }
        else
        {
            user.Status = "processing"; user.LeaseToken = leaseToken;
            user.LeaseUntil = leaseUntil; user.CaptureExpiresAt = expiresAt;
            db.SessionCaptureEvents.Add(user);
        }
        await db.SaveChangesAsync(token);
        // Keep the reserving connection open so its backend PID identifies the exact blocked call.
        await db.Database.OpenConnectionAsync(token);
        var reservingPid = await db.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token);
        await using var blocker = fixture.CreateDb(true);
        await using var transaction = await blocker.Database.BeginTransactionAsync(token);
        var blockingPid = await blocker.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(token);
        if (ownerKind == "route")
            await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM public.depot_access_keys WHERE id = {key.Id} FOR UPDATE", token);
        else
            await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT workspace_id FROM public.workspace_access_grants WHERE depot_access_key_id = {key.Id} AND workspace_id = {workspace.Id} FOR UPDATE", token);
        var budget = new SessionCaptureCallBudget(db, options);
        var reservation = budget.TryReserveAsync(new(depot.Id, key.Id, ownerKind,
            ownerKind == "route" ? route.Id : user.Id, leaseToken),
            ownerKind == "route" ? ProviderCallKind.RoutingChat : ProviderCallKind.ExtractionChat, token);
        // A database-observed wait is the barrier: no scheduling assumptions or blind sleeps.
        while (!await blocker.Database.SqlQuery<bool>($"SELECT {blockingPid} = ANY(pg_blocking_pids({reservingPid})) AS \"Value\"").SingleAsync(token))
        {
            Assert.False(reservation.IsCompleted, "Reservation must block on the authorization row lock.");
            await Task.Delay(10, token);
        }
        if (boundary == "disabled") options.CurrentValue = new() { Enabled = false };
        else
        {
            var blockedAt = await blocker.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(token);
            Assert.True(blockedAt < boundaryAt, "Authorization lock must be reached before the boundary.");
            // Wait exactly to the database deadline, bounded by the cancellation token.
            await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_sleep(GREATEST(0, EXTRACT(EPOCH FROM ({boundaryAt} - clock_timestamp())))::double precision)", token);
        }
        await transaction.CommitAsync(token);
        Assert.Equal(boundary == "deadline" ? "expired" : "lease_lost", (await reservation).Status);
        db.ChangeTracker.Clear();
        if (ownerKind == "route")
        {
            var stored = await db.SessionCaptureTurnRoutes.SingleAsync(token);
            Assert.Equal(0, stored.ProviderCallCount);
            Assert.Null(stored.LastProviderCallAt);
            Assert.Equal(0, stored.AttemptCount);
        }
        else
        {
            var stored = await db.SessionCaptureEvents.SingleAsync(token);
            Assert.Equal(0, stored.ProviderCallCount);
            Assert.Null(stored.LastProviderCallAt);
            Assert.Equal(0, stored.AttemptCount);
        }
    }

    [PostgreSqlFact]
    public async Task Finish_requires_pair_token_and_aggregates_durable_partial_success()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var queue = new SessionCaptureRepository(db, protector, new Monitor(), TimeProvider.System);
        var user = Message(depot, workspace, key, "turn", "user", protector);
        var assistant = Message(depot, workspace, key, "turn", "assistant", protector);
        await queue.EnqueueTurnAsync(user, assistant, default);
        var turn = Assert.IsType<CapturedTurn>(await queue.ClaimAsync(default));
        await queue.FinishAsync(turn with { LeaseToken = Guid.NewGuid() }, new(SessionCaptureDisposition.Completed, "wrong"), default);
        Assert.Equal("processing", user.Status);
        user.CandidateOutcomesJson = "[{\"index\":0,\"outcome\":\"created\",\"contextId\":\"" + Guid.NewGuid() + "\"}]";
        await db.SaveChangesAsync();
        await queue.FinishAsync(turn, new(SessionCaptureDisposition.Failed, "retry_exhausted", "chat_unavailable"), default);
        Assert.All(new[] { user, assistant }, e =>
        {
            Assert.Equal("failed", e.Status); Assert.Equal("saved", e.ResultCode);
            Assert.Equal("chat_unavailable", e.LastErrorCode); Assert.Null(e.LeaseToken);
        });
    }

    [PostgreSqlFact]
    public async Task Auto_tombstones_block_explicit_retries_and_session_start_including_terminal_routes()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var queue = new SessionCaptureRepository(db, protector, new Monitor(), TimeProvider.System);
        var user = Message(depot, workspace, key, "turn", "user", protector);
        // Simulate a promoted message plus its permanent route authority.
        db.SessionCaptureEvents.Add(user);
        var route = Route(depot, key, "turn"); route.State = "promoted"; route.WorkspaceId = workspace.Id;
        db.SessionCaptureTurnRoutes.Add(route); await db.SaveChangesAsync();
        var conflict = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => queue.EnqueueAsync(user, default));
        Assert.Equal(ApplicationErrorCodes.SessionCaptureConflict, conflict.ErrorCode);
        var start = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => new SessionRepository(db).StartAsync(new()
        {
            Id = Guid.NewGuid(), DepotId = depot.Id, WorkspaceId = workspace.Id, AccessKeyId = key.Id,
            Agent = "test", ClientSessionId = "client", CreatedAt = DateTimeOffset.UtcNow
        }, default));
        Assert.Equal(ApplicationErrorCodes.SessionConflict, start.ErrorCode);
    }

    [PostgreSqlFact]
    public async Task Sensitive_partner_purges_both_phases_and_cannot_cancel_changed_content()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var queue = new SessionCaptureRepository(db, protector, new Monitor(), TimeProvider.System);
        var user = Message(depot, workspace, key, "turn", "user", protector);
        await queue.EnqueueAsync(user, default);
        var assistant = Message(depot, workspace, key, "turn", "assistant", protector);
        assistant.EncryptedContent = null; assistant.ResultCode = "skipped_sensitive";
        await queue.EnqueueAsync(assistant, default);
        Assert.All(new[] { user, assistant }, e =>
        {
            Assert.Equal("completed", e.Status); Assert.Equal("skipped_sensitive", e.ResultCode);
            Assert.Null(e.EncryptedContent); Assert.NotNull(e.ContentPurgedAt); Assert.Null(e.LeaseToken);
        });
        var changed = Message(depot, workspace, key, "turn", "user", protector);
        changed.ContentHash = new string('F', 64); changed.EncryptedContent = null; changed.ResultCode = "skipped_sensitive";
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => queue.EnqueueAsync(changed, default));
        Assert.Equal(ApplicationErrorCodes.SessionCaptureConflict, error.ErrorCode);
    }

    [PostgreSqlFact]
    public async Task Inbox_and_explicit_queue_share_capacity()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var route = Route(depot, key, "auto");
        db.SessionCaptureTurnRoutes.Add(route);
        db.SessionCaptureInboxEvents.Add(new()
        {
            Id = Guid.NewGuid(), TurnRouteId = route.Id, DepotId = depot.Id, AccessKeyId = key.Id,
            Agent = route.Agent, ClientSessionId = route.ClientSessionId, TurnId = route.TurnId,
            EventId = "auto-user", Phase = "user", EncryptedContent = "protected", ContentHash = new string('A', 64), CreatedAt = route.CreatedAt
        });
        await db.SaveChangesAsync();
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var queue = new SessionCaptureRepository(db, protector, new Monitor { CurrentValue = new() { Enabled = true, MaxPendingEventsPerKey = 1 } }, TimeProvider.System);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => queue.EnqueueAsync(Message(depot, workspace, key, "explicit", "user", protector), default));
        Assert.Equal(ApplicationErrorCodes.SessionCaptureCapacity, error.ErrorCode);
    }

    [PostgreSqlFact]
    public async Task A_busy_key_does_not_block_another_keys_pair()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var first = await SeedAsync(db);
        var second = await SeedAsync(db);
        var protector = new SessionContentProtector(new EphemeralDataProtectionProvider());
        var queue = new SessionCaptureRepository(db, protector, new Monitor(), TimeProvider.System);
        await queue.EnqueueTurnAsync(Message(first.Item1, first.Item2, first.Item3, "first", "user", protector),
            Message(first.Item1, first.Item2, first.Item3, "first", "assistant", protector), default);
        await queue.EnqueueTurnAsync(Message(second.Item1, second.Item2, second.Item3, "second", "user", protector),
            Message(second.Item1, second.Item2, second.Item3, "second", "assistant", protector), default);
        await using var blocker = fixture.CreateDb(true);
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        var lockKey = $"session-capture:{first.Item1.Id}:{first.Item3.Id}";
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var turn = Assert.IsType<CapturedTurn>(await queue.ClaimAsync(timeout.Token));
        Assert.Equal(second.Item3.Id, turn.AccessKeyId);
    }

    [PostgreSqlFact]
    public async Task Upgrade_backfills_only_user_provider_counts_and_keeps_old_processing_lease()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync("20261022000000_RemoveApplicationSettingKeyPrefix");
        await using var db = fixture.CreateDb(true);
        var (depot, workspace, key) = await SeedAsync(db);
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var leaseUntil = now.AddMinutes(2);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.session_capture_events
                (id,depot_id,access_key_id,workspace_id,agent,client_session_id,turn_id,event_id,phase,
                 encrypted_content,content_hash,status,attempt_count,next_attempt_at,created_at,lease_until)
            VALUES ({userId},{depot.Id},{key.Id},{workspace.Id},'test','client','turn','user','user',
                'legacy-ciphertext',{new string('A', 64)},'processing',2,{now},{now},{leaseUntil})
            """);
        await db.Database.MigrateAsync();
        var user = await db.SessionCaptureEvents.SingleAsync();
        Assert.Equal(2, user.ProviderCallCount);
        Assert.InRange(user.LastProviderCallAt!.Value, now.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Null(user.CaptureExpiresAt); Assert.Null(user.LeaseToken); Assert.Null(user.EncryptedExtractionPlan);
        Assert.Equal("legacy-ciphertext", user.EncryptedContent);
        Assert.Equal(leaseUntil.ToUnixTimeMilliseconds(), user.LeaseUntil!.Value.ToUnixTimeMilliseconds());
    }

    private static async Task<(Depot, Workspace, DepotAccessKey)> SeedAsync(ContextDepotDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.NewGuid(), "Capture", now);
        var workspace = new Workspace(Guid.NewGuid(), depot.Id, null, "Capture", "capture", null, now);
        var key = new DepotAccessKey(Guid.NewGuid(), depot.Id, "Capture", Guid.NewGuid().ToString("N")[..12], "hash", now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, workspace.Id, now));
        db.AddRange(depot, workspace, key); await db.SaveChangesAsync();
        return (depot, workspace, key);
    }

    private static SessionCaptureEvent Message(Depot depot, Workspace workspace, DepotAccessKey key, string turn, string phase, ISessionContentProtector protector) => new()
    {
        Id = Guid.NewGuid(), DepotId = depot.Id, AccessKeyId = key.Id, WorkspaceId = workspace.Id,
        Agent = "test", ClientSessionId = "client", TurnId = turn, EventId = turn + "-" + phase, Phase = phase,
        EncryptedContent = protector.Protect(phase), ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(phase))),
        CreatedAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1)
    };

    private static SessionCaptureTurnRoute Route(Depot depot, DepotAccessKey key, string turn) => new()
    {
        Id = Guid.NewGuid(), DepotId = depot.Id, AccessKeyId = key.Id, Agent = "test", ClientSessionId = "client", TurnId = turn,
        State = "pending", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(24), NextAttemptAt = DateTimeOffset.UtcNow
    };

    private sealed class Monitor : IOptionsMonitor<SessionCaptureOptions>
    {
        public SessionCaptureOptions CurrentValue { get; set; } = new() { Enabled = true };
        public SessionCaptureOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SessionCaptureOptions, string?> listener) => null;
    }
}
