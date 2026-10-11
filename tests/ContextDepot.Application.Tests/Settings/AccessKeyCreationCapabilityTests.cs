using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Domain.Depots;
using Moq;

namespace ContextDepot.Application.Tests.Settings;

public sealed class AccessKeyCreationCapabilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Empty_initial_grants_require_creation_capability(bool capability, bool allowed)
    {
        var keys = new Mock<IAccessKeyRepository>(MockBehavior.Strict);
        var secrets = new Mock<IAccessKeySecretGenerator>(MockBehavior.Strict);
        var ids = new Mock<IIdGenerator>(MockBehavior.Strict);
        var depot = new Mock<ICurrentDepotContext>(); depot.SetupGet(item => item.DepotId).Returns(Guid.CreateVersion7());
        var access = new Mock<IWorkspaceAccessContext>(); access.SetupGet(item => item.HasUnrestrictedAccess).Returns(true);
        DepotAccessKey? stored = null;
        if (allowed)
        {
            secrets.Setup(item => item.Generate()).Returns(new GeneratedAccessKey("example-secret", "example-prefix", "example-hash"));
            ids.Setup(item => item.NewId()).Returns(Guid.CreateVersion7());
            keys.Setup(item => item.CreateAsync(It.IsAny<DepotAccessKey>(), default)).ReturnsAsync((DepotAccessKey key, CancellationToken _) =>
            {
                stored = key;
                return new AccessKeyDto(key.Id, key.Name, key.KeyPrefix, key.CreatedAt, null, null, [], key.CanCreateRootWorkspaces, "0");
            });
        }
        var service = new AccessKeyAppService(keys.Object, secrets.Object, depot.Object, access.Object, ids.Object, TimeProvider.System);
        if (allowed)
        {
            await service.CreateAsync(new("Creator", [], capability), default);
            Assert.True(stored!.CanCreateRootWorkspaces);
            Assert.Empty(stored.WorkspaceGrants);
            Assert.Equal(0, stored.AccessVersion);
        }
        else
        {
            Assert.Equal(ApplicationErrorCodes.InvalidRequest,
                (await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.CreateAsync(new("Ordinary", [], capability), default))).ErrorCode);
            keys.VerifyNoOtherCalls(); secrets.VerifyNoOtherCalls(); ids.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [InlineData(false, false, null, true)]
    [InlineData(false, true, null, false)]
    [InlineData(true, false, null, false)]
    [InlineData(true, true, null, false)]
    [InlineData(true, true, 0L, true)]
    [InlineData(false, true, 0L, true)]
    [InlineData(true, true, 1L, false)]
    public void Version_is_required_for_current_or_new_creation_capability(bool current, bool requested, long? expected, bool allowed)
    {
        var key = Key(current);
        var command = new AccessKeyAccessUpdate([Guid.CreateVersion7()], requested, expected);
        if (allowed) Assert.Equal(requested, AccessKeyAccessPolicy.Approve(key, command));
        else Assert.Equal(ApplicationErrorCodes.SettingsConflict,
            Assert.Throws<ContextDepotApplicationException>(() => AccessKeyAccessPolicy.Approve(key, command)).ErrorCode);
    }

    [Fact]
    public void Concurrent_auto_grant_version_prevents_a_stale_editor_from_replacing_grants()
    {
        var key = Key(true);
        key.AdvanceAccessVersion();
        Assert.Equal(ApplicationErrorCodes.SettingsConflict, Assert.Throws<ContextDepotApplicationException>(() =>
            AccessKeyAccessPolicy.Approve(key, new([], true, 0))).ErrorCode);
        Assert.Equal(ApplicationErrorCodes.InvalidRequest, Assert.Throws<ContextDepotApplicationException>(() =>
            AccessKeyAccessPolicy.Approve(key, new([], false, 1))).ErrorCode);
        Assert.True(AccessKeyAccessPolicy.Approve(key, new([], null, 1)));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("+1")]
    [InlineData("9223372036854775808")]
    public async Task Invalid_versions_are_rejected_before_persistence(string expected)
    {
        var keys = new Mock<IAccessKeyRepository>(MockBehavior.Strict);
        var access = new Mock<IWorkspaceAccessContext>(); access.SetupGet(item => item.HasUnrestrictedAccess).Returns(true);
        var depot = new Mock<ICurrentDepotContext>(); depot.SetupGet(item => item.DepotId).Returns(Guid.CreateVersion7());
        var service = new AccessKeyAppService(keys.Object, new Mock<IAccessKeySecretGenerator>().Object,
            depot.Object, access.Object, new Mock<IIdGenerator>().Object, TimeProvider.System);
        Assert.Equal(ApplicationErrorCodes.InvalidRequest, (await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.SetGrantsAsync(Guid.CreateVersion7(), new([], true, expected), default))).ErrorCode);
        keys.VerifyNoOtherCalls();
    }

    [Fact]
    public void Last_used_does_not_change_access_version_and_revoke_is_idempotent()
    {
        var key = Key(true);
        key.MarkUsed(DateTimeOffset.UtcNow);
        Assert.Equal(0, key.AccessVersion);
        key.Revoke(DateTimeOffset.UtcNow);
        key.Revoke(DateTimeOffset.UtcNow);
        Assert.Equal(1, key.AccessVersion);
        Assert.Equal(ApplicationErrorCodes.SettingsConflict, Assert.Throws<ContextDepotApplicationException>(() =>
            AccessKeyAccessPolicy.Approve(key, new([], true, 1))).ErrorCode);
    }

    private static DepotAccessKey Key(bool capability) => new(Guid.CreateVersion7(), Guid.CreateVersion7(),
        "Client", "example-prefix", "example-hash", DateTimeOffset.UtcNow, capability);
}
