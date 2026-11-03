using System.Data.Common;
using System.Text.Json;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ContextDepot.Infrastructure.Tests;

[Collection(CapturePostgreSqlCollection.Name)]
public sealed class CaptureContextPersistenceTests(CapturePostgreSqlFixture fixture)
{
    [CapturePostgreSqlFact]
    public async Task Context_evidence_and_outcome_rollback_together_then_retry_is_idempotent()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        await fixture.ResolveAsync(locator, seed.WorkspaceId);
        CapturedTurn turn;
        await using (var db = fixture.CreateDb())
        {
            Assert.True(await fixture.Inbox(db).PromoteAsync(locator, default));
            var queue = new SessionCaptureRepository(db, fixture.Protector, CapturePostgreSqlFixture.Options(), TimeProvider.System);
            turn = Assert.IsType<CapturedTurn>(await queue.ClaimAsync(default));
        }
        var validator = new ExtractionPlanValidator(new HighConfidenceSecretDetector());
        var plan = validator.Validate([new MemoryCandidate("fact", "project.storage.database", Summary: "The project uses PostgreSQL for persistent storage.", Quotes: [turn.UserContent])], turn.UserContent);
        Assert.Single(plan.Candidates);
        SessionCapturePlanRepository Repository(ContextDepotDbContext db) => new(db, fixture.Protector, fixture.EvidenceProtector,
            CapturePostgreSqlFixture.Options(), new HighConfidenceSecretDetector());
        PreparedCaptureContext Prepare()
        {
            var candidate = plan.Candidates[0];
            var context = new ContextItem(Guid.NewGuid(), seed.DepotId, seed.WorkspaceId, ContextKind.Fact,
                candidate.Key, null, candidate.Summary, DateTimeOffset.UtcNow);
            context.ConfigureProvenance(VerificationStatus.Unknown, ProvenanceTrust.Asserted, SourceType.Agent,
                turn.Agent, $"session-capture:{turn.UserEventId}:{turn.AssistantEventId}");
            return new(context);
        }
        await using (var db = fixture.CreateDb())
        {
            Assert.Equal("committed", (await Repository(db).StorePlanAsync(turn, plan, default)).Status);
            Assert.Equal(1, (await db.SessionCaptureEvents.SingleAsync(x => x.Id == turn.UserEventId)).PersistenceAttemptCount);
            Assert.Equal("committed", (await Repository(db).StorePlanAsync(turn, plan, default)).Status);
            Assert.Equal(1, (await db.SessionCaptureEvents.SingleAsync(x => x.Id == turn.UserEventId)).PersistenceAttemptCount);
        }
        await using (var failing = fixture.CreateDb(new CaptureSaveFault(db => db.ChangeTracker.Entries<ContextCaptureEvidence>().Any())))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Repository(failing).SaveCandidateAsync(turn, 0, Prepare(), default));
            Assert.Equal("capture-test-injected-save-failure", error.Message);
        }
        await using (var verify = fixture.CreateDb())
        {
            Assert.Empty(await verify.ContextItems.ToArrayAsync());
            Assert.Empty(await verify.ContextCaptureEvidence.ToArrayAsync());
            var user = await verify.SessionCaptureEvents.SingleAsync(m => m.Id == turn.UserEventId);
            using var outcomes = JsonDocument.Parse(user.CandidateOutcomesJson);
            Assert.Equal(0, outcomes.RootElement.GetArrayLength());
            Assert.NotNull(user.EncryptedExtractionPlan);
            Assert.Equal(1, user.PersistenceAttemptCount);
        }
        CaptureCandidateOutcome saved;
        await using (var retry = fixture.CreateDb()) saved = await Repository(retry).SaveCandidateAsync(turn, 0, Prepare(), default);
        Assert.Equal("created", saved.Outcome);
        await using (var retry = fixture.CreateDb()) Assert.Equal(saved, await Repository(retry).SaveCandidateAsync(turn, 0, Prepare(), default));
        await using (var verify = fixture.CreateDb())
        {
            Assert.Equal(saved.ContextId, (await verify.ContextItems.SingleAsync()).Id);
            var evidence = await verify.ContextCaptureEvidence.SingleAsync();
            Assert.Equal(saved.ContextId, evidence.ContextId);
            Assert.Equal(turn.UserEventId, evidence.UserEventId);
            Assert.Contains(turn.UserContent, fixture.EvidenceProtector.Unprotect(evidence.EncryptedFragments));
            using var outcomes = JsonDocument.Parse((await verify.SessionCaptureEvents.SingleAsync(m => m.Id == turn.UserEventId)).CandidateOutcomesJson);
            Assert.Equal(1, outcomes.RootElement.GetArrayLength());
            Assert.Equal(saved.ContextId, outcomes.RootElement[0].GetProperty("contextId").GetGuid());
        }
    }

    [CapturePersistencePostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Candidate_waiting_for_workspace_rechecks_expiration_and_live_settings(bool disableInsteadOfExpire)
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        await fixture.ResolveAsync(locator, seed.WorkspaceId);
        var options = new MutableCapturePersistenceOptions();
        CapturedTurn turn;
        await using (var db = fixture.CreateDb())
        {
            Assert.True(await fixture.Inbox(db).PromoteAsync(locator, default));
            turn = Assert.IsType<CapturedTurn>(await new SessionCaptureRepository(db, fixture.Protector, options, TimeProvider.System).ClaimAsync(default));
        }
        var plan = new ExtractionPlanValidator(new HighConfidenceSecretDetector()).Validate(
            [new MemoryCandidate("fact", "project.storage.database", Summary: "The project uses PostgreSQL for persistent storage.", Quotes: [turn.UserContent])], turn.UserContent);
        Assert.Single(plan.Candidates);
        SessionCapturePlanRepository Repository(ContextDepotDbContext db) => new(db, fixture.Protector,
            fixture.EvidenceProtector, options, new HighConfidenceSecretDetector());
        await using (var db = fixture.CreateDb())
        {
            Assert.Equal("committed", (await Repository(db).StorePlanAsync(turn, plan, default)).Status);
            Assert.Equal(1, (await db.SessionCaptureEvents.SingleAsync(x => x.Id == turn.UserEventId)).PersistenceAttemptCount);
        }
        var candidate = plan.Candidates[0];
        var item = new ContextItem(Guid.NewGuid(), seed.DepotId, seed.WorkspaceId, ContextKind.Fact,
            candidate.Key, null, candidate.Summary, DateTimeOffset.UtcNow);
        item.ConfigureProvenance(VerificationStatus.Unknown, ProvenanceTrust.Asserted, SourceType.Agent,
            turn.Agent, $"session-capture:{turn.UserEventId}:{turn.AssistantEventId}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        await using var blocker = fixture.CreateDb();
        await using var transaction = await blocker.Database.BeginTransactionAsync(ct);
        await blocker.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM public.workspaces WHERE id = {seed.WorkspaceId} FOR UPDATE").ToArrayAsync(ct);
        var blockerPid = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
        await using var observer = fixture.CreateDb();
        if (!disableInsteadOfExpire)
        {
            // Real database time, with enough runway to reach the lock before the deadline.
            await observer.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.session_capture_events SET capture_expires_at = clock_timestamp() + interval '5 seconds' WHERE id IN ({turn.UserEventId}, {turn.AssistantEventId})", ct);
        }
        var signal = new CaptureCommandAttempt("public.workspaces", "FOR UPDATE");
        await using var worker = fixture.CreateDb(signal);
        var save = Repository(worker).SaveCandidateAsync(turn, 0, new(item), ct);
        try
        {
            var workerPid = await signal.Attempted.Task.WaitAsync(ct);
            await CaptureCommandAttempt.WaitForBlockAsync(observer, workerPid, blockerPid, ct);
            Assert.False(save.IsCompleted);
            if (disableInsteadOfExpire)
            {
                options.Disable();
            }
            else
            {
                Assert.True(await observer.Database.SqlQuery<bool>($"SELECT capture_expires_at > clock_timestamp() AS \"Value\" FROM public.session_capture_events WHERE id = {turn.UserEventId}").SingleAsync(ct));
                // Only after observing the blocked workspace attempt do we wait out its deadline.
                await observer.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_sleep(GREATEST(0, EXTRACT(EPOCH FROM (capture_expires_at - clock_timestamp()))) + 0.01) FROM public.session_capture_events WHERE id = {turn.UserEventId}", ct);
                Assert.True(await observer.Database.SqlQuery<bool>($"SELECT capture_expires_at <= clock_timestamp() AS \"Value\" FROM public.session_capture_events WHERE id = {turn.UserEventId}").SingleAsync(ct));
            }
        }
        finally
        {
            await transaction.RollbackAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => save.WaitAsync(ct));
        Assert.Equal(ApplicationErrorCodes.SessionCaptureLeaseLost, error.ErrorCode);
        await using var verify = fixture.CreateDb();
        Assert.Empty(await verify.ContextItems.ToArrayAsync(ct));
        Assert.Empty(await verify.ContextCaptureEvidence.ToArrayAsync(ct));
        var events = await verify.SessionCaptureEvents.ToArrayAsync(ct);
        Assert.Equal(2, events.Length);
        Assert.All(events, message =>
        {
            using var outcomes = JsonDocument.Parse(message.CandidateOutcomesJson);
            Assert.Equal(0, outcomes.RootElement.GetArrayLength());
        });
    }
}

internal sealed class CapturePersistencePostgreSqlTheoryAttribute : TheoryAttribute
{
    public CapturePersistencePostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CapturePostgreSqlFixture.ConnectionVariable)))
            Skip = $"Real PostgreSQL requires {CapturePostgreSqlFixture.ConnectionVariable}; skipped is not acceptance.";
    }
}

internal sealed class MutableCapturePersistenceOptions : IOptionsMonitor<SessionCaptureOptions>
{
    private SessionCaptureOptions value = new() { Enabled = true };
    public SessionCaptureOptions CurrentValue => Volatile.Read(ref value);
    public SessionCaptureOptions Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<SessionCaptureOptions, string?> listener) => null;
    public void Disable() => Volatile.Write(ref value, new SessionCaptureOptions { Enabled = false });
}

// Signal the actual command attempt, then independently verify PostgreSQL is waiting on our blocker.
internal sealed class CaptureCommandAttempt(string table, string lockClause) : DbCommandInterceptor
{
    public TaskCompletionSource<int> Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(table, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(lockClause, StringComparison.OrdinalIgnoreCase))
            Attempted.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
        return ValueTask.FromResult(result);
    }

    public static async Task WaitForBlockAsync(ContextDepotDbContext observer, int workerPid, int blockerPid, CancellationToken ct)
    {
        while (!await observer.Database.SqlQuery<bool>($"SELECT {blockerPid} = ANY(pg_blocking_pids({workerPid})) AS \"Value\"").SingleAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }
}
