using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

[Collection(CapturePostgreSqlCollection.Name)]
public sealed class SessionCaptureStatusRepositoryTests(CapturePostgreSqlFixture fixture)
{
    [CapturePostgreSqlFact]
    public async Task Promoted_status_uses_queue_body_not_purged_inbox_and_denies_other_key_or_depot()
    {
        var seed = await fixture.SeedAsync();
        var locator = await fixture.AcceptPairAsync(seed);
        await fixture.ResolveAsync(locator, seed.WorkspaceId);
        Guid receipt;
        Guid otherKey;
        await using (var db = fixture.CreateDb())
        {
            Assert.True(await fixture.Inbox(db).PromoteAsync(locator, default));
            receipt = await db.SessionCaptureInboxEvents.Where(m => m.Phase == "user").Select(m => m.Id).SingleAsync();
            otherKey = Guid.NewGuid();
            var key = new DepotAccessKey(otherKey, seed.DepotId, "Other authorized key", "other-capture", "test-only", DateTimeOffset.UtcNow);
            key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, seed.DepotId, seed.WorkspaceId, DateTimeOffset.UtcNow));
            db.Add(key);
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.CreateDb())
        {
            var repository = new SessionCaptureStatusRepository(db);
            var message = await repository.GetAsync(seed.DepotId, seed.KeyId, new(receipt, null), default);
            var turn = await repository.GetAsync(seed.DepotId, seed.KeyId, new(null, locator.RouteId), default);
            Assert.NotNull(message);
            Assert.NotNull(turn);
            Assert.Equal("promoted", message.RoutingState);
            Assert.Equal("pending", message.ProcessingState);
            Assert.Equal(turn.Messages, message.Messages);
            Assert.All(message.Messages, m => Assert.False(m.ContentPurged));
            Assert.Null(await repository.GetAsync(seed.DepotId, otherKey, new(receipt, null), default));
            Assert.Null(await repository.GetAsync(seed.DepotId, otherKey, new(null, locator.RouteId), default));
            Assert.Null(await repository.GetAsync(Guid.NewGuid(), seed.KeyId, new(receipt, null), default));
        }
        await using (var db = fixture.CreateDb())
            await db.SessionCaptureEvents.Where(m => m.Id == receipt).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.EncryptedContent, (string?)null).SetProperty(m => m.ContentPurgedAt, DateTimeOffset.UtcNow));
        await using (var db = fixture.CreateDb())
        {
            var status = await new SessionCaptureStatusRepository(db).GetAsync(seed.DepotId, seed.KeyId, new(receipt, null), default);
            Assert.NotNull(status);
            Assert.True(status.Messages.Single(m => m.Phase == "user").ContentPurged);
            Assert.False(status.Messages.Single(m => m.Phase == "assistant").ContentPurged);
        }
    }
}
