using ContextDepot.Application.AgentSessions.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

[Collection(CapturePostgreSqlCollection.Name)]
public sealed class SessionCaptureRoutingConcurrencyTests(CapturePostgreSqlFixture fixture)
{
    [CapturePostgreSqlFact]
    public async Task Independent_workers_claim_one_token_and_expired_token_cannot_complete()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        using var barrier = new Barrier(2);
        async Task<RouteLease?> Claim() => await Task.Run(async () =>
        {
            await using var db = fixture.CreateDb();
            await db.Database.OpenConnectionAsync();
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
            return await fixture.Inbox(db).TryClaimRouteAsync(locator, default);
        });
        var claims = await Task.WhenAll(Claim(), Claim());
        var old = Assert.Single(claims, x => x is not null)!;
        await using (var db = fixture.CreateDb())
            await db.SessionCaptureTurnRoutes.Where(r => r.Id == locator.RouteId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await using var worker = fixture.CreateDb();
        var inbox = fixture.Inbox(worker);
        var current = Assert.IsType<RouteLease>(await inbox.TryClaimRouteAsync(locator, default));
        Assert.NotEqual(old.LeaseToken, current.LeaseToken);
        Assert.False(await inbox.CompleteRouteAsync(old, new("resolved", seed.WorkspaceId), default));
        Assert.True(await inbox.CompleteRouteAsync(current, new("resolved", seed.WorkspaceId), default));
        await using var verify = fixture.CreateDb();
        Assert.Equal("resolved", (await verify.SessionCaptureTurnRoutes.SingleAsync()).State);
    }

    [CapturePostgreSqlFact]
    public async Task Promotion_save_failure_rolls_back_pair_and_purge_then_restart_promotes_exactly_once()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        await fixture.ResolveAsync(locator, seed.WorkspaceId);
        await using (var failing = fixture.CreateDb(new CaptureSaveFault(db =>
            db.ChangeTracker.Entries<ContextDepot.Domain.AgentSessions.SessionCaptureEvent>().Any())))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Inbox(failing).PromoteAsync(locator, default));
            Assert.Equal("capture-test-injected-save-failure", error.Message);
        }
        await using (var verify = fixture.CreateDb())
        {
            Assert.Empty(await verify.SessionCaptureEvents.ToArrayAsync());
            Assert.Equal("resolved", (await verify.SessionCaptureTurnRoutes.SingleAsync()).State);
            Assert.All(await verify.SessionCaptureInboxEvents.ToArrayAsync(), m => { Assert.NotNull(m.EncryptedContent); Assert.Null(m.ContentPurgedAt); });
        }
        await using (var restart = fixture.CreateDb()) Assert.True(await fixture.Inbox(restart).PromoteAsync(locator, default));
        await using (var restart = fixture.CreateDb()) Assert.True(await fixture.Inbox(restart).PromoteAsync(locator, default));
        await using (var verify = fixture.CreateDb())
        {
            var route = await verify.SessionCaptureTurnRoutes.SingleAsync();
            Assert.Equal("promoted", route.State);
            var messages = await verify.SessionCaptureEvents.OrderBy(m => m.Id).ToArrayAsync();
            Assert.Equal(2, messages.Length);
            Assert.Equal((await verify.SessionCaptureInboxEvents.OrderBy(m => m.Id).Select(m => m.Id).ToArrayAsync()), messages.Select(m => m.Id));
            Assert.All(messages, m => { Assert.NotNull(m.EncryptedContent); Assert.Equal(route.ExpiresAt, m.CaptureExpiresAt); Assert.Equal(0, m.ProviderCallCount); });
            Assert.All(await verify.SessionCaptureInboxEvents.ToArrayAsync(), m => Assert.Null(m.EncryptedContent));
        }
    }

    [CapturePostgreSqlFact]
    public async Task Revoked_workspace_grant_prevents_promotion_and_purges_inbox()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        await fixture.ResolveAsync(locator, seed.WorkspaceId);
        await using (var revoke = fixture.CreateDb()) await revoke.WorkspaceAccessGrants.ExecuteDeleteAsync();
        await using (var worker = fixture.CreateDb()) Assert.False(await fixture.Inbox(worker).PromoteAsync(locator, default));
        await using var verify = fixture.CreateDb();
        Assert.Empty(await verify.SessionCaptureEvents.ToArrayAsync());
        Assert.Equal("skipped_unauthorized", (await verify.SessionCaptureTurnRoutes.SingleAsync()).ResultCode);
        Assert.All(await verify.SessionCaptureInboxEvents.ToArrayAsync(), m => Assert.Null(m.EncryptedContent));
    }
}
