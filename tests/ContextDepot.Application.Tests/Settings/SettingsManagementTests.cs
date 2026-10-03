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
    private readonly Mock<IInferenceSettingsRepository> inference = new(MockBehavior.Strict);
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
        error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => InferenceService().GetAsync(default));
        Assert.Equal(ApplicationErrorCodes.SettingsForbidden, error.ErrorCode);
        keys.VerifyNoOtherCalls(); inference.VerifyNoOtherCalls(); secrets.VerifyNoOtherCalls();
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
        var revision = DateTimeOffset.UtcNow;
        var request = new SaveInferenceProviderRequest(null, "Provider", endpoint, "example-key", null, null, null, revision, revision);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => InferenceService().SaveProviderAsync(request, default));
        Assert.Equal(ApplicationErrorCodes.InvalidInferenceConfiguration, error.ErrorCode);
        inference.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(16001, 30)]
    [InlineData(3, 0)]
    [InlineData(3, 301)]
    public async Task InvalidDimensionsOrTimeoutDoNotReachPersistence(int dimensions, int timeout)
    {
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => InferenceService().SaveAsync("embedding",
            ValidInference() with { Dimensions = dimensions, TimeoutSeconds = timeout }, default));
        inference.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChatDoesNotAcceptVectorDimensions()
    {
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => InferenceService().SaveAsync("chat", ValidInference(), default));
        inference.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidChatNormalizesModelWithoutEditingProviderCredentials()
    {
        var request = ValidInference() with { Dimensions = null };
        var result = new InferenceRouteDto("chat", "Provider", "openai-compatible", "https://example.test/v1", "model", null, 30, true,
            request.UpdatedAt, "configured", "not-applicable", false);
        inference.Setup(x => x.SaveAsync("chat", It.Is<SaveInferenceRouteRequest>(value => value.ProviderId == request.ProviderId && value.Model == "model"), default)).ReturnsAsync(result);
        Assert.Same(result, await InferenceService().SaveAsync("chat", request, default));
    }

    private static SaveInferenceRouteRequest ValidInference() => new(Guid.CreateVersion7(), " model ", 3, 30, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task ProviderSaveNormalizesSharedConnectionAndTwoDifferentModelsAtomically()
    {
        var revision = DateTimeOffset.UtcNow;
        var request = new SaveInferenceProviderRequest(null, " Provider ", "https://example.test/v1", " example-key ", null,
            new(" embed-model ", 3, 30), new(" chat-model ", null, 60), revision, revision);
        var result = new InferenceProviderSettingsDto([], []);
        inference.Setup(x => x.SaveProviderAsync(It.Is<SaveInferenceProviderRequest>(value =>
            value.Name == "Provider" && value.ApiKey == "example-key" && value.Embedding!.Model == "embed-model" &&
            value.Chat!.Model == "chat-model" && value.Embedding.Dimensions == 3 && value.Chat.Dimensions == null), default))
            .ReturnsAsync(result);
        Assert.Same(result, await InferenceService().SaveProviderAsync(request, default));
        Assert.DoesNotContain("example-key", request.ToString());
        inference.VerifyAll();
    }

    [Theory]
    [InlineData("https://user:secret@example.test", 3, null)]
    [InlineData("https://example.test?key=secret", 3, null)]
    [InlineData("https://example.test", 0, null)]
    [InlineData("https://example.test", 3, 3)]
    public async Task InvalidProviderCannotPartiallySaveEitherModel(string endpoint, int dimensions, int? chatDimensions)
    {
        var revision = DateTimeOffset.UtcNow;
        var request = new SaveInferenceProviderRequest(null, "Provider", endpoint, "example-key", null,
            new("embed", dimensions, 30), new("chat", chatDimensions, 30), revision, revision);
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => InferenceService().SaveProviderAsync(request, default));
        inference.VerifyNoOtherCalls();
    }
    private AccessKeyAppService KeyService() => new(keys.Object, secrets.Object, depot.Object, access.Object, ids.Object, TimeProvider.System);

    [Theory]
    [InlineData("deepseek", "https://api.deepseek.com/v1/", true)]
    [InlineData("unknown", "https://example.test/v1/", false)]
    [InlineData("azure-openai", "https://resource.openai.azure.com/", false)]
    [InlineData("azure-openai", "https://resource.openai.azure.com/openai/deployments/model?api-version=1", false)]
    public async Task UnsupportedProviderCapabilitiesAndAzureLegacyEndpointsNeverReachPersistence(string kind, string endpoint, bool embedding)
    {
        var revision = DateTimeOffset.UtcNow;
        var request = new SaveInferenceProviderRequest(null, "Provider", endpoint, "example-key", null,
            embedding ? new("model", 3, 30) : null, new("deployment", null, 30), revision, revision, kind);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => InferenceService().SaveProviderAsync(request, default));
        Assert.Equal(ApplicationErrorCodes.InvalidInferenceConfiguration, error.ErrorCode);
        inference.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("openai", "https://api.openai.com/v1/", true)]
    [InlineData("azure-openai", "https://resource.openai.azure.com/openai/v1/", true)]
    [InlineData("deepseek", "https://api.deepseek.com/v1/", false)]
    public async Task PresetConnectionsSupportTheirCapabilities(string kind, string endpoint, bool embedding)
    {
        var revision = DateTimeOffset.UtcNow;
        var request = new SaveInferenceProviderRequest(null, "Provider", endpoint, "example-key", null,
            embedding ? new("deployment", 3, 30) : null, new("deployment", null, 30), revision, revision, kind);
        var result = new InferenceProviderSettingsDto([], []);
        inference.Setup(x => x.SaveProviderAsync(It.Is<SaveInferenceProviderRequest>(value => value.Kind == kind), default)).ReturnsAsync(result);
        Assert.Same(result, await InferenceService().SaveProviderAsync(request, default));
    }

    private InferenceSettingsAppService InferenceService() => new(inference.Object, depot.Object, access.Object);
}
