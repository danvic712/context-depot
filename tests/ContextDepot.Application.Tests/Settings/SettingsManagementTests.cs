using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Domain.Depots;
using Moq;

namespace ContextDepot.Application.Tests.Settings;

public sealed class SettingsManagementTests
{
    private readonly Guid depotId = Guid.CreateVersion7();
    private readonly Guid workspaceId = Guid.CreateVersion7();
    private readonly Mock<IAccessKeyRepository> keys = new(MockBehavior.Strict);
    private readonly Mock<IAccessKeySecretGenerator> secrets = new(MockBehavior.Strict);
    private readonly Mock<IAiSettingsRepository> ai = new(MockBehavior.Strict);
    private readonly Mock<ICurrentDepotContext> depot = new();
    private readonly Mock<IWorkspaceAccessContext> access = new();
    private readonly Mock<IIdGenerator> ids = new(MockBehavior.Strict);

    public SettingsManagementTests()
    {
        depot.SetupGet(x => x.DepotId).Returns(depotId);
        access.SetupGet(x => x.HasUnrestrictedAccess).Returns(true);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task McpKeysCannotManageSettings(bool unrestricted, bool hasKey)
    {
        access.SetupGet(x => x.HasUnrestrictedAccess).Returns(unrestricted);
        access.SetupGet(x => x.DepotAccessKeyId).Returns(hasKey ? Guid.CreateVersion7() : null);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => KeyService().ListAsync(default));
        Assert.Equal(ApplicationErrorCodes.SettingsForbidden, error.ErrorCode);
        error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => AiService().GetAsync(default));
        Assert.Equal(ApplicationErrorCodes.SettingsForbidden, error.ErrorCode);
        keys.VerifyNoOtherCalls(); ai.VerifyNoOtherCalls(); secrets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateIssuesSecretOnceAndStoresOnlyHashWithDistinctGrants()
    {
        var id = Guid.CreateVersion7();
        var generated = new GeneratedAccessKey("example-secret", "cdk_example", "example-hash");
        secrets.Setup(x => x.Generate()).Returns(generated);
        ids.Setup(x => x.NewId()).Returns(id);
        keys.Setup(x => x.WorkspacesBelongToDepotAsync(depotId, It.Is<IReadOnlyList<Guid>>(value => value.SequenceEqual(new[] { workspaceId })), default)).ReturnsAsync(true);
        DepotAccessKey? stored = null;
        keys.Setup(x => x.CreateAsync(It.IsAny<DepotAccessKey>(), default)).ReturnsAsync((DepotAccessKey key, CancellationToken _) =>
        {
            stored = key;
            return new AccessKeyDto(key.Id, key.Name, key.KeyPrefix, key.CreatedAt, null, null, [workspaceId]);
        });
        var result = await KeyService().CreateAsync(new CreateAccessKeyRequest("  My client  ", [workspaceId, workspaceId]), default);
        Assert.Equal(generated.Plaintext, result.Secret);
        Assert.Equal(id, stored!.Id); Assert.Equal(depotId, stored.DepotId);
        Assert.Equal("My client", stored.Name); Assert.Equal(generated.SecretHash, stored.SecretHash);
        Assert.Single(stored.WorkspaceGrants); Assert.Equal(workspaceId, stored.WorkspaceGrants.Single().WorkspaceId);
        Assert.DoesNotContain(generated.Plaintext, result.ToString());
        Assert.DoesNotContain(generated.Plaintext, generated.ToString());
    }

    [Fact]
    public async Task ForeignWorkspaceCannotBeGrantedOrIssueASecret()
    {
        keys.Setup(x => x.WorkspacesBelongToDepotAsync(depotId, It.IsAny<IReadOnlyList<Guid>>(), default)).ReturnsAsync(false);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            KeyService().CreateAsync(new CreateAccessKeyRequest("Client", [workspaceId]), default));
        Assert.Equal(ApplicationErrorCodes.InvalidRequest, error.ErrorCode);
        secrets.VerifyNoOtherCalls(); ids.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNamesNeverIssueKeys(string name)
    {
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            KeyService().CreateAsync(new CreateAccessKeyRequest(name, [workspaceId]), default));
        keys.VerifyNoOtherCalls(); secrets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmptyGrantsCannotIssueAKey()
    {
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            KeyService().CreateAsync(new CreateAccessKeyRequest("Client", []), default));
        keys.VerifyNoOtherCalls(); secrets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingOrForeignKeyHasSameNotFoundResult()
    {
        var id = Guid.CreateVersion7();
        keys.Setup(x => x.RevokeAsync(depotId, id, It.IsAny<DateTimeOffset>(), default)).ReturnsAsync((AccessKeyDto?)null);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => KeyService().RevokeAsync(id, default));
        Assert.Equal(ApplicationErrorCodes.AccessKeyNotFound, error.ErrorCode);
    }

    [Theory]
    [InlineData("ftp://example.test")]
    [InlineData("https://user:secret@example.test")]
    [InlineData("https://example.test?key=secret")]
    [InlineData("/v1")]
    public async Task UnsafeEndpointsDoNotReachPersistence(string endpoint)
    {
        var request = ValidAi() with { Endpoint = endpoint };
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => AiService().SaveAsync("embedding", request, default));
        Assert.Equal(ApplicationErrorCodes.InvalidAiConfiguration, error.ErrorCode);
        ai.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(16001, 30)]
    [InlineData(3, 0)]
    [InlineData(3, 301)]
    public async Task InvalidDimensionsOrTimeoutDoNotReachPersistence(int dimensions, int timeout)
    {
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => AiService().SaveAsync("embedding",
            ValidAi() with { Dimensions = dimensions, TimeoutSeconds = timeout }, default));
        ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChatDoesNotAcceptVectorDimensions()
    {
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => AiService().SaveAsync("chat", ValidAi(), default));
        ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidChatNormalizesInputAndBlankKeyMeansKeepExisting()
    {
        var request = ValidAi() with { Dimensions = null, ApiKey = "  " };
        var result = new AiRouteDto("chat", "Provider", "openai-compatible", "https://example.test/v1", "model", null, 30, true,
            request.UpdatedAt, "configured", "not-applicable", false);
        ai.Setup(x => x.SaveAsync("chat", It.Is<SaveAiRouteRequest>(value => value.ApiKey == null && value.ProviderName == "Provider" && value.Model == "model"), default)).ReturnsAsync(result);
        Assert.Same(result, await AiService().SaveAsync("chat", request, default));
        Assert.DoesNotContain("example-secret", (request with { ApiKey = "example-secret" }).ToString());
    }

    private static SaveAiRouteRequest ValidAi() => new(" Provider ", "https://example.test/v1", " model ", 3, 30, "example-key", DateTimeOffset.UtcNow);
    private AccessKeyAppService KeyService() => new(keys.Object, secrets.Object, depot.Object, access.Object, ids.Object, TimeProvider.System);
    private AiSettingsAppService AiService() => new(ai.Object, depot.Object, access.Object);
}
