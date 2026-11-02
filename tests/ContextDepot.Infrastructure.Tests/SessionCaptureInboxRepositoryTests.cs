using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.AgentSessions;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ContextDepot.Infrastructure.Tests;

[Collection(CapturePostgreSqlCollection.Name)]
public sealed class SessionCaptureInboxRepositoryTests(CapturePostgreSqlFixture fixture)
{
    [CapturePostgreSqlFact]
    public async Task Accept_waits_for_concurrent_key_revocation_then_rejects_without_inbox_or_route()
    {
        var seed = await fixture.SeedAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        await using var revoke = fixture.CreateDb();
        await using var transaction = await revoke.Database.BeginTransactionAsync(ct);
        await revoke.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.depot_access_keys SET revoked_at = clock_timestamp() WHERE id = {seed.KeyId}", ct);
        var blockerPid = ((NpgsqlConnection)revoke.Database.GetDbConnection()).ProcessID;
        var signal = new CaptureCommandAttempt("public.depot_access_keys", "FOR SHARE");
        await using var worker = fixture.CreateDb(signal);
        var accept = fixture.Inbox(worker).AcceptAsync(CapturePostgreSqlFixture.Envelope(
            fixture.Message(seed, "user"), fixture.Message(seed, "assistant")), ct);
        try
        {
            var workerPid = await signal.Attempted.Task.WaitAsync(ct);
            await using var observer = fixture.CreateDb();
            await CaptureCommandAttempt.WaitForBlockAsync(observer, workerPid, blockerPid, ct);
            Assert.False(accept.IsCompleted);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync().WaitAsync(TimeSpan.FromSeconds(10));
            throw;
        }
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => accept.WaitAsync(ct));
        Assert.Equal(ApplicationErrorCodes.SessionCaptureUnavailable, error.ErrorCode);
        await using var verify = fixture.CreateDb();
        Assert.NotNull((await verify.DepotAccessKeys.SingleAsync(ct)).RevokedAt);
        Assert.Empty(await verify.SessionCaptureInboxEvents.ToArrayAsync(ct));
        Assert.Empty(await verify.SessionCaptureTurnRoutes.ToArrayAsync(ct));
        Assert.Empty(await verify.SessionCaptureEvents.ToArrayAsync(ct));
    }

    [CapturePostgreSqlFact]
    public async Task Assistant_first_waits_for_user_and_retries_compare_hash_not_ciphertext()
    {
        var seed = await fixture.SeedAsync();
        await using var db = fixture.CreateDb();
        var inbox = fixture.Inbox(db);
        var first = await inbox.AcceptAsync(CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "assistant")), default);
        var locator = new RouteLocator(seed.DepotId, seed.KeyId, first.TurnReceiptId!.Value);
        Assert.Null(await inbox.TryClaimRouteAsync(locator, default));
        var retry = await inbox.AcceptAsync(CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "assistant")), default);
        Assert.Equal(first.ReceiptId, retry.ReceiptId);
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => inbox.AcceptAsync(
            CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "assistant", "Different text.")), default));
        await inbox.AcceptAsync(CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "user")), default);
        Assert.NotNull(await inbox.TryClaimRouteAsync(locator, default));
        Assert.Equal(2, await db.SessionCaptureInboxEvents.CountAsync());
    }

    [CapturePostgreSqlFact]
    public async Task Sensitive_second_message_purges_partner_and_terminal_late_message_has_no_body()
    {
        var seed = await fixture.SeedAsync();
        await using var db = fixture.CreateDb();
        var inbox = fixture.Inbox(db);
        await inbox.AcceptAsync(CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "user")), default);
        var sensitive = fixture.Message(seed, "assistant", "sensitive-test-marker");
        sensitive.EncryptedContent = null;
        var receipt = await inbox.AcceptAsync(new([sensitive], true, DateTimeOffset.UtcNow.AddHours(24)), default);
        Assert.Equal("skipped_sensitive", receipt.ResultCode);
        db.ChangeTracker.Clear();
        Assert.All(await db.SessionCaptureInboxEvents.ToArrayAsync(), m => { Assert.Null(m.EncryptedContent); Assert.NotNull(m.ContentPurgedAt); });
        Assert.Null((await db.SessionCaptureTurnRoutes.SingleAsync()).LeaseToken);
        Assert.Empty(await db.SessionCaptureEvents.ToArrayAsync());

        // A terminal single-phase turn accepts the missing phase only as a hash tombstone.
        var early = fixture.Message(seed, "user", "sensitive-test-marker", "terminal-turn");
        early.EncryptedContent = null;
        var terminal = await inbox.AcceptAsync(new([early], true, DateTimeOffset.UtcNow.AddHours(24)), default);
        var late = await inbox.AcceptAsync(CapturePostgreSqlFixture.Envelope(
            fixture.Message(seed, "assistant", "This must never be retained.", "terminal-turn")), default);
        Assert.Equal(terminal.TurnReceiptId, late.TurnReceiptId);
        Assert.Equal("skipped_sensitive", late.ResultCode);
        db.ChangeTracker.Clear();
        var lateTombstone = await db.SessionCaptureInboxEvents.SingleAsync(m => m.Id == late.ReceiptId);
        Assert.Null(lateTombstone.EncryptedContent);
        Assert.NotNull(lateTombstone.ContentPurgedAt);
    }

    [CapturePostgreSqlFact]
    public async Task Pair_capacity_failure_leaves_neither_route_nor_message()
    {
        var seed = await fixture.SeedAsync();
        await using var db = fixture.CreateDb();
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => fixture.Inbox(db, 1).AcceptAsync(
            CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "user"), fixture.Message(seed, "assistant")), default));
        await using var verify = fixture.CreateDb();
        Assert.Empty(await verify.SessionCaptureTurnRoutes.ToArrayAsync());
        Assert.Empty(await verify.SessionCaptureInboxEvents.ToArrayAsync());
    }

    [CapturePostgreSqlFact]
    public async Task Explicit_and_auto_modes_cannot_claim_each_others_identity()
    {
        var seed = await fixture.SeedAsync();
        await fixture.AcceptPairAsync(seed);
        await using var db = fixture.CreateDb();
        var queue = new SessionCaptureRepository(db, fixture.Protector, CapturePostgreSqlFixture.Options(), TimeProvider.System);
        SessionCaptureEvent Explicit(string turn) => new()
        {
            Id = Guid.NewGuid(), DepotId = seed.DepotId, AccessKeyId = seed.KeyId, WorkspaceId = seed.WorkspaceId,
            Agent = "codex", ClientSessionId = "capture-session", TurnId = turn, EventId = turn + ":user", Phase = "user",
            ContentHash = fixture.Message(seed, "user").ContentHash, EncryptedContent = fixture.Protector.Protect("I use PostgreSQL for persistent storage."),
            CreatedAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow
        };
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => queue.EnqueueAsync(Explicit("turn-1"), default));
        db.ChangeTracker.Clear();
        await queue.EnqueueAsync(Explicit("explicit-turn"), default);
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => fixture.Inbox(db).AcceptAsync(
            CapturePostgreSqlFixture.Envelope(fixture.Message(seed, "user", turn: "explicit-turn")), default));
        await using var verify = fixture.CreateDb();
        Assert.Single(await verify.SessionCaptureTurnRoutes.ToArrayAsync());
        Assert.Single(await verify.SessionCaptureEvents.ToArrayAsync());
    }
}
