using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Infrastructure.AgentSessions;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

[Collection(CapturePostgreSqlCollection.Name)]
public sealed class SessionCaptureBudgetTests(CapturePostgreSqlFixture fixture)
{
    [CapturePostgreSqlFact]
    public async Task Budget_one_defers_chat_but_persisted_vector_survives_for_next_hour()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        RouteLease lease;
        await using (var db = fixture.CreateDb()) lease = Assert.IsType<RouteLease>(await fixture.Inbox(db).TryClaimRouteAsync(locator, default));
        var now = DateTimeOffset.UtcNow;
        CaptureWorkLease Work(RouteLease l) => new(seed.DepotId, seed.KeyId, "route", locator.RouteId, l.LeaseToken);
        async Task<BudgetReservation> Reserve(RouteLease l, ProviderCallKind kind)
        {
            await using var db = fixture.CreateDb();
            return await new SessionCaptureCallBudget(db, CapturePostgreSqlFixture.Options(calls: 1)).TryReserveAsync(Work(l), kind, default);
        }
        Assert.Equal("granted", (await Reserve(lease, ProviderCallKind.RoutingEmbedding)).Status);
        var vector = fixture.Protector.Protect("[0.25,0.75]");
        await using (var db = fixture.CreateDb()) Assert.True(await fixture.Inbox(db).StoreRoutingVectorAsync(lease, vector, lease.UserContentHash, "test-profile", default));
        var wait = await Reserve(lease, ProviderCallKind.RoutingChat);
        Assert.Equal("wait", wait.Status);
        Assert.NotNull(wait.WaitUntil);
        await using (var db = fixture.CreateDb())
        {
            Assert.True(await fixture.Inbox(db).CompleteRouteAsync(lease, new("budget_deferred", NotBefore: wait.WaitUntil), default));
            var route = await db.SessionCaptureTurnRoutes.AsNoTracking().SingleAsync();
            Assert.Equal(1, route.ProviderCallCount);
            Assert.Equal(0, route.AttemptCount);
            Assert.Equal(vector, route.EncryptedRoutingVector);
        }
        // Inbox uses the database clock. Age only the persisted budget timestamp, and make the
        // deferred route due; never pretend advancing a fake clock advances PostgreSQL leases.
        await using (var db = fixture.CreateDb())
            await db.SessionCaptureTurnRoutes.ExecuteUpdateAsync(s => s
                .SetProperty(r => r.LastProviderCallAt, now.AddHours(-2))
                .SetProperty(r => r.NextAttemptAt, now.AddMinutes(-1)));
        await using (var db = fixture.CreateDb()) lease = Assert.IsType<RouteLease>(await fixture.Inbox(db).TryClaimRouteAsync(locator, default));
        Assert.Equal(vector, lease.EncryptedRoutingVector);
        Assert.Equal("[0.25,0.75]", fixture.Protector.Unprotect(lease.EncryptedRoutingVector!));
        Assert.Equal("test-profile", lease.RoutingEmbeddingFingerprint);
        Assert.Equal("granted", (await Reserve(lease, ProviderCallKind.RoutingChat)).Status);
        await using (var db = fixture.CreateDb()) Assert.True(await fixture.Inbox(db).CompleteRouteAsync(lease, new("resolved", seed.WorkspaceId), default));
        await using var verify = fixture.CreateDb();
        var final = await verify.SessionCaptureTurnRoutes.SingleAsync();
        Assert.Equal(2, final.ProviderCallCount); // one embedding + one chat, not a second embedding
        Assert.Equal("resolved", final.State);
    }

    [CapturePostgreSqlFact]
    public async Task Concurrent_last_budget_slot_grants_exactly_one_request()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        RouteLease lease;
        await using (var db = fixture.CreateDb()) lease = Assert.IsType<RouteLease>(await fixture.Inbox(db).TryClaimRouteAsync(locator, default));
        using var barrier = new Barrier(2);
        Task<BudgetReservation> Reserve(ProviderCallKind kind) => Task.Run(async () =>
        {
            await using var db = fixture.CreateDb();
            await db.Database.OpenConnectionAsync();
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
            return await new SessionCaptureCallBudget(db, CapturePostgreSqlFixture.Options(calls: 1))
                .TryReserveAsync(new(seed.DepotId, seed.KeyId, "route", locator.RouteId, lease.LeaseToken), kind, default);
        });
        var results = await Task.WhenAll(Reserve(ProviderCallKind.RoutingEmbedding), Reserve(ProviderCallKind.RoutingChat));
        Assert.Single(results, r => r.Status == "granted");
        Assert.Single(results, r => r.Status == "wait");
        await using var verify = fixture.CreateDb();
        Assert.Equal(1, (await verify.SessionCaptureTurnRoutes.SingleAsync()).ProviderCallCount);
    }
}
