using ContextDepot.Application.DataProtection;
using ContextDepot.Application.DataProtection.Enums;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Setup;
using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Setup.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Workspaces;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.Setup;

public sealed class SetupAppServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-04T12:00:00Z");
    private static CompleteSetupRequest Draft(string name = "Knowledge", string path = "knowledge") => new(new(name, path), []);

    [Fact]
    public async Task ReadingStatusAndCatalogDoesNotPersistDrafts()
    {
        var repository = new SetupMemoryRepository(); var service = Create(repository);
        Assert.Equal("pending", (await service.GetAsync(default)).State);
        Assert.Equal(2, (await service.GetInferenceSettingsAsync(default)).Routes.Count);
        Assert.Equal(0, repository.MutationCount); Assert.Null(repository.Created);
    }

    [Fact]
    public async Task FinalConfirmationCreatesTheSpaceAndCompletesInOneMutationWithOptionalFeaturesSkipped()
    {
        var repository = new SetupMemoryRepository(); var service = Create(repository);
        var result = await service.CompleteAsync(new(new(" Personal knowledge ", " personal ", " Notes "), []), default);
        Assert.Equal("completed", result.Status.State); Assert.Equal("review", result.Status.NextStep); Assert.Null(result.AccessKey);
        Assert.Equal("Personal knowledge", result.Status.Workspace!.Name); Assert.Equal("personal", result.Status.Workspace.Path);
        Assert.Equal("Notes", result.Status.Workspace.Description);
        Assert.Equal(7, repository.Created!.Depot.Id.Version); Assert.Equal(7, repository.Created.Workspace.Id.Version);
        Assert.Equal(repository.Created.Depot.Id, repository.Created.Workspace.DepotId); Assert.Null(repository.Created.Workspace.ParentWorkspaceId);
        Assert.Equal(Now, repository.Created.Workspace.CreatedAt); Assert.Equal(Now, repository.Snapshot.Installation.CompletedAt);
        Assert.Equal(1, repository.MutationCount); Assert.Empty(repository.Created.Providers); Assert.Null(repository.Created.AccessKey);
    }

    [Fact]
    public async Task ModelsAndKeyAreIncludedInTheFinalTransactionAndSecretIsReturnedOnlyOnce()
    {
        var repository = new SetupMemoryRepository(); var service = Create(repository);
        var provider = new SaveInferenceProviderRequest(null, " OpenAI ", "https://api.openai.com/v1/", "fixture-api-key", null,
            new(" embedding-model ", 8, 30), new(" chat-model ", null, 30), Now, Now, "openai");
        var request = Draft() with { Providers = [provider], AccessKeyName = " Client " };
        var result = await service.CompleteAsync(request, default);
        Assert.NotNull(result.AccessKey); Assert.Equal("Client", result.AccessKey.Key.Name);
        Assert.Equal(new[] { result.Status.Workspace!.Id }, result.AccessKey.Key.WorkspaceIds);
        Assert.Equal("encrypted-fixture", Assert.Single(repository.Created!.Providers).ProtectedApiKey);
        Assert.Equal("embedding-model", repository.Created.Routes.Single(route => route.Capability == "embedding").ModelName);
        Assert.Equal("chat-model", repository.Created.Routes.Single(route => route.Capability == "chat").ModelName);
        Assert.DoesNotContain("fixture-api-key", request.ToString()); Assert.DoesNotContain(result.AccessKey.Secret, result.ToString());
        var repeated = await service.CompleteAsync(request, default);
        Assert.Equal(result.Status, repeated.Status); Assert.Null(repeated.AccessKey); Assert.Equal(1, repository.CreatedCount);
    }

    [Theory]
    [InlineData("", "knowledge")][InlineData("Knowledge", "")][InlineData("Knowledge", "Knowledge")]
    [InlineData("Knowledge", "projects/knowledge")][InlineData("Knowledge", "../knowledge")][InlineData("Knowledge", "knowledge--base")]
    public async Task InvalidWorkspaceIsRejectedBeforeMutation(string name, string path)
    {
        var repository = new SetupMemoryRepository();
        await AssertCode(ApplicationErrorCodes.InvalidSetupWorkspace, () => Create(repository).CompleteAsync(Draft(name, path), default));
        Assert.Equal(0, repository.MutationCount);
    }

    [Fact]
    public async Task InvalidModelsDuplicateCapabilitiesAndKeyNamesDoNotBeginPersistence()
    {
        var repository = new SetupMemoryRepository(); var service = Create(repository);
        var provider = new SaveInferenceProviderRequest(null, "Provider", "https://example.test/v1/", "fixture", null, new("model", 8, 30), null, Now, Now);
        await AssertCode(ApplicationErrorCodes.InvalidInferenceConfiguration, () => service.CompleteAsync(Draft() with { Providers = [provider, provider] }, default));
        await AssertCode(ApplicationErrorCodes.InvalidInferenceConfiguration, () => service.CompleteAsync(Draft() with { Providers = [provider with { Endpoint = "https://user:secret@example.test/v1" }] }, default));
        await AssertCode(ApplicationErrorCodes.InvalidRequest, () => service.CompleteAsync(Draft() with { AccessKeyName = " " }, default));
        await AssertCode(ApplicationErrorCodes.InvalidRequest, () => service.CompleteAsync(Draft() with { AccessKeyName = new string('a',201) }, default));
        Assert.Equal(0, repository.MutationCount);
    }

    [Fact]
    public async Task SourceSafetyAndLengthLimitsStillApplyToFinalWorkspace()
    {
        var repository = new SetupMemoryRepository(); var service = Create(repository);
        await AssertCode(ApplicationErrorCodes.InvalidSetupWorkspace, () => service.CompleteAsync(Draft(new string('a',201)), default));
        await AssertCode(ApplicationErrorCodes.InvalidSetupWorkspace, () => service.CompleteAsync(Draft() with { Workspace = new("Knowledge", "knowledge", new string('a',2001)) }, default));
        await AssertCode(ApplicationErrorCodes.SecretContentRejected, () => service.CompleteAsync(Draft() with { Workspace = new("Knowledge", "knowledge", "-----BEGIN PRIVATE KEY-----") }, default));
        Assert.Equal(0, repository.MutationCount);
    }

    [Fact]
    public async Task DeploymentDepotIdAndRecordedLegacyWorkspaceArePreserved()
    {
        var id = Guid.CreateVersion7(); var repository = new SetupMemoryRepository();
        var result = await Create(repository,id.ToString()).CompleteAsync(Draft(), default);
        Assert.Equal(id, repository.Created!.Depot.Id);
        var legacy = new SetupMemoryRepository(); var workspace = new Workspace(Guid.CreateVersion7(),id,null,"Old","old",null,Now);
        legacy.Snapshot.Installation.Begin(id,workspace.Id,Now);
        legacy.Snapshot = legacy.Snapshot with { Workspace=workspace, InitialDepotExists=true, HasAnyDepot=true };
        var completed = await Create(legacy,id.ToString()).CompleteAsync(Draft("Updated","updated"), default);
        Assert.Equal(workspace.Id, completed.Status.Workspace!.Id); Assert.Equal("updated", completed.Status.Workspace.Path);
        Assert.True(legacy.Created!.ExistingWorkspace);
        await AssertCode(ApplicationErrorCodes.SetupUnavailable, () => Create(new SetupMemoryRepository(),"invalid").CompleteAsync(Draft(), default));
    }

    [Fact]
    public async Task UnrecordedResourcesAndStaleProviderRevisionsAreRejected()
    {
        var repository = new SetupMemoryRepository(); repository.Snapshot=repository.Snapshot with {HasAnyDepot=true};
        await AssertCode(ApplicationErrorCodes.SetupConflict, () => Create(repository).CompleteAsync(Draft(), default));
        repository=new SetupMemoryRepository();
        var stale=new SaveInferenceProviderRequest(Guid.CreateVersion7(),"Provider","https://example.test/","fixture",Now,null,null,Now,Now);
        await AssertCode(ApplicationErrorCodes.SettingsConflict, () => Create(repository).CompleteAsync(Draft() with {Providers=[stale]}, default));
    }

    [Fact]
    public async Task CompletedInstallationDoesNotReopenOrIssueAnotherKeyAfterResourcesAreDeleted()
    {
        var repository = new SetupMemoryRepository(); repository.Snapshot.Installation.Complete(Now);
        var result = await Create(repository).CompleteAsync(Draft() with {AccessKeyName="Client"}, default);
        Assert.Equal("completed",result.Status.State); Assert.Null(result.Status.Workspace); Assert.Null(result.AccessKey); Assert.Null(repository.Created);
    }

    private static async Task AssertCode(string code, Func<Task<SetupCompletionDto>> action) =>
        Assert.Equal(code,(await Assert.ThrowsAsync<ContextDepotApplicationException>(action)).ErrorCode);
    private static ISetupAppService Create(SetupMemoryRepository repository,string? depotId=null)
    {
        var protector=new Mock<ISecretProtector>();
        protector.Setup(service=>service.Protect(It.IsAny<string>(),SecretProtectionPurpose.InferenceProviderApiKey)).Returns("encrypted-fixture");
        string? plaintext="fixture";
        protector.Setup(service=>service.TryUnprotect(It.IsAny<string>(),SecretProtectionPurpose.InferenceProviderApiKey,out plaintext)).Returns(true);
        var generator=new Mock<IAccessKeySecretGenerator>(MockBehavior.Strict);
        generator.Setup(service=>service.Generate()).Returns(new GeneratedAccessKey("issued-test-only","test-prefix","stored-hash"));
        return new SetupAppService(repository,new SetupTestIdGenerator(),new SetupFixedClock(Now),Options.Create(new SetupOptions{DepotId=depotId}),
            new SourceSafetyService(new HighConfidenceSecretDetector(),new ProvenancePolicy()),protector.Object,generator.Object);
    }
}
