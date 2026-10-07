using ContextDepot.Application.Setup;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Infrastructure.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Setup.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

public sealed class SetupRepositoryTests
{
    [PostgreSqlFact]
    public async Task EmptyDatabaseMigrationSeedsOnePendingUuidV7Singleton()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb();
        await db.Database.MigrateAsync();
        var state = Assert.Single(await db.InstallationStates.AsNoTracking().ToArrayAsync());
        Assert.Equal(7, state.Id.Version);
        Assert.Equal(InstallationState.InstallationScope, state.Scope);
        Assert.Equal(InstallationState.Pending, state.State);
        Assert.Null(state.InitialDepotId);
        Assert.Null(state.InitialWorkspaceId);
        Assert.Null(state.CompletedAt);
        Assert.False(state.InferenceReviewed);
        Assert.False(state.AccessKeyReviewed);
        Assert.Empty(await db.Depots.ToArrayAsync());

        db.InstallationStates.Add(new InstallationState(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task ExistingMultipleDepotMigrationCompletesWithoutChangingResourcesOrSettings()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync("20261003023404_SeedInferenceProviders");
        await using var db = fixture.CreateDb(true);
        var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.CreateVersion7(), "Existing knowledge", now);
        db.Depots.Add(depot);
        db.Depots.Add(new Depot(Guid.CreateVersion7(), "Second existing knowledge", now));
        var workspace = new Workspace(Guid.CreateVersion7(), depot.Id, null, "Existing workspace", "existing", "Keep", now);
        db.Workspaces.Add(workspace);
        var settingsBefore = await db.ApplicationSettings.AsNoTracking().OrderBy(setting => setting.Key)
            .Select(setting => new { setting.Key, setting.ValueJson }).ToArrayAsync();
        var providersBefore = await db.InferenceProviders.AsNoTracking().OrderBy(provider => provider.Id)
            .Select(provider => provider.Id).ToArrayAsync();
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();

        var state = Assert.Single(await db.InstallationStates.AsNoTracking().ToArrayAsync());
        Assert.Equal(InstallationState.Completed, state.State);
        Assert.True(state.InferenceReviewed);
        Assert.True(state.AccessKeyReviewed);
        Assert.NotNull(state.CompletedAt);
        Assert.Null(state.InitialDepotId);
        Assert.Null(state.InitialWorkspaceId);
        Assert.Equal(2, await db.Depots.CountAsync());
        Assert.Equal(workspace.Id, (await db.Workspaces.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(settingsBefore, await db.ApplicationSettings.AsNoTracking().OrderBy(setting => setting.Key)
            .Select(setting => new { setting.Key, setting.ValueJson }).ToArrayAsync());
        Assert.Equal(providersBefore, await db.InferenceProviders.AsNoTracking().OrderBy(provider => provider.Id)
            .Select(provider => provider.Id).ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task NoConfigurationIsWrittenUntilFinalConfirmationAndAllResourcesAreCommittedTogether()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb();
        var service = CreateService(db);
        Assert.Equal("pending", (await service.GetAsync(default)).State);
        var catalog = await service.GetInferenceSettingsAsync(default);
        Assert.Empty(await db.Depots.ToArrayAsync()); Assert.Empty(await db.DepotAccessKeys.ToArrayAsync());
        Assert.False(await db.InferenceProviders.AnyAsync(provider => provider.ProtectedApiKey != null));
        var embedding = catalog.Routes.Single(route => route.Capability == "embedding");
        var chat = catalog.Routes.Single(route => route.Capability == "chat");
        var preset = catalog.Providers.Single(provider => provider.Kind == "openai");
        var provider = new SaveInferenceProviderRequest(preset.Id, "OpenAI", "https://api.openai.com/v1/", "fixture-api-key", preset.UpdatedAt,
            new("embedding-model", 8, 30), new("chat-model", null, 30), embedding.UpdatedAt, chat.UpdatedAt, "openai");
        var request = new CompleteSetupRequest(new("Research", "research", "Final description"), [provider], "MCP client");
        var result = await service.CompleteAsync(request, default);
        Assert.Equal("completed", result.Status.State); Assert.NotNull(result.AccessKey);
        await using var verify = fixture.CreateDb(true);
        Assert.Single(await verify.Depots.ToArrayAsync()); Assert.Single(await verify.Workspaces.ToArrayAsync());
        var key = Assert.Single(await verify.DepotAccessKeys.Include(key => key.WorkspaceGrants).ToArrayAsync());
        Assert.Equal(result.Status.Workspace!.Id, Assert.Single(key.WorkspaceGrants).WorkspaceId);
        Assert.Equal(2, await verify.InferenceRoutes.CountAsync(route => route.ProviderId == preset.Id));
        var saved = await verify.InferenceProviders.SingleAsync(item => item.Id == preset.Id);
        Assert.NotEqual("fixture-api-key", saved.ProtectedApiKey); Assert.Contains("embedding-model", (await verify.InferenceRoutes.SingleAsync(route => route.Capability == "embedding")).ModelName);
        var identity = await new DepotAccessKeyAuthenticator(verify, new DepotAccessKeySecretHasher(), TimeProvider.System).AuthenticateAsync(result.AccessKey.Secret);
        Assert.Equal(key.Id, identity!.DepotAccessKeyId);
        var repeated = await service.CompleteAsync(request, default);
        Assert.Null(repeated.AccessKey); Assert.Equal(result.Status, repeated.Status);
        Assert.Equal(1, await verify.DepotAccessKeys.CountAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentFinalConfirmationsCreateOneSpaceAndOneKeyAndReturnTheSecretOnce()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var first = fixture.CreateDb(); await using var second = fixture.CreateDb();
        var request = new CompleteSetupRequest(new("Knowledge", "knowledge"), [], "Client");
        var results = await Task.WhenAll(CreateService(first).CompleteAsync(request, default), CreateService(second).CompleteAsync(request, default));
        Assert.Equal(results[0].Status, results[1].Status); Assert.Single(results, result => result.AccessKey is not null);
        await using var verify = fixture.CreateDb(true);
        Assert.Equal(1, await verify.Depots.CountAsync()); Assert.Equal(1, await verify.Workspaces.CountAsync()); Assert.Equal(1, await verify.DepotAccessKeys.CountAsync());
    }

    [PostgreSqlFact]
    public async Task FailedFinalWriteRollsBackSpaceModelsKeyAndInstallationAndAllowsRetryInSameScope()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync(); await using var db = fixture.CreateDb();
        var repository = new SetupRepository(db); var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.CreateVersion7(), "Failed", now);
        var workspace = new Workspace(Guid.CreateVersion7(), depot.Id, null, "Failed", "failed", null, now);
        var hasher = new DepotAccessKeySecretHasher(); var secret = hasher.Generate();
        var key = new DepotAccessKey(Guid.CreateVersion7(), depot.Id, "Failed", secret.Prefix, secret.SecretHash, now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, Guid.CreateVersion7(), now));
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.MutateAsync(snapshot =>
        {
            var provider = snapshot.Providers.Single(provider => provider.Kind == "openai"); provider.ProtectedApiKey = "failed-fixture-ciphertext";
            var route = snapshot.Routes.Single(route => route.Capability == "embedding");
            route.Provider = provider; route.ProviderId = provider.Id; route.ModelName = "failed-model"; route.Dimensions = 8;
            snapshot.Installation.Begin(depot.Id, workspace.Id, now); snapshot.Installation.Complete(now);
            return new SetupResources(depot, workspace) { Providers = [provider], Routes = snapshot.Routes, AccessKey = key };
        }, default));
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified);
        Assert.Empty(await db.Depots.ToArrayAsync()); Assert.Empty(await db.DepotAccessKeys.ToArrayAsync());
        Assert.False(await db.InferenceProviders.AnyAsync(provider => provider.ProtectedApiKey != null));
        Assert.Equal("pending", (await repository.GetAsync(default)).Installation.State);
        Assert.False(await db.InferenceRoutes.AnyAsync(route => route.ProviderId != null));
        var result = await CreateService(db).CompleteAsync(new(new("Knowledge", "knowledge"), []), default);
        Assert.Equal("completed", result.Status.State); Assert.Equal(1, await db.Depots.CountAsync());
    }

    [PostgreSqlFact]
    public async Task LegacyPartialSetupCanBeEditedAtFinalConfirmationWithoutReplacingItsSpace()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync(); await using var db = fixture.CreateDb(true);
        var now = DateTimeOffset.UtcNow; var depot = new Depot(Guid.CreateVersion7(), "Legacy", now);
        var workspace = new Workspace(Guid.CreateVersion7(), depot.Id, null, "Old", "old", "Old", now);
        await new SetupRepository(db).MutateAsync(snapshot =>
        { snapshot.Installation.Begin(depot.Id, workspace.Id, now); return new SetupResources(depot, workspace); }, default);
        var service = CreateService(db, depot.Id.ToString());
        Assert.Equal("workspace", (await service.GetAsync(default)).NextStep);
        var result = await service.CompleteAsync(new(new("Edited", "edited", "Updated"), []), default);
        Assert.Equal(workspace.Id, result.Status.Workspace!.Id); Assert.Equal("edited", result.Status.Workspace.Path);
        Assert.Equal(1, await db.Depots.CountAsync());
        db.ChangeTracker.Clear(); Assert.Equal("Updated", (await db.Workspaces.SingleAsync()).Description);
    }

    [PostgreSqlFact]
    public async Task DatabaseRejectsRecordedWorkspaceFromAnotherDepot()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync(); await using var db = fixture.CreateDb(true);
        var result = await CreateService(db).CompleteAsync(new(new("Knowledge", "knowledge"), []), default);
        var originalDepotId = (await db.InstallationStates.AsNoTracking().SingleAsync()).InitialDepotId!.Value;
        var now = DateTimeOffset.UtcNow;
        var foreign = new Depot(Guid.CreateVersion7(), "Other", now);
        var workspace = new Workspace(Guid.CreateVersion7(), foreign.Id, null, "Other", "other", null, now);
        db.AddRange(foreign, workspace); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => new SetupRepository(db).MutateAsync(snapshot =>
        { snapshot.Installation.Begin(originalDepotId, workspace.Id, now); return null; }, default));
        Assert.Equal(result.Status.Workspace!.Id, (await new SetupRepository(db).GetAsync(default)).Workspace!.Id);
    }

    private static SetupAppService CreateService(ContextDepotDbContext db, string? configuredDepotId = null) =>
        new(new SetupRepository(db), new GuidV7IdGenerator(), TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new SetupOptions { DepotId = configuredDepotId }),
            new SourceSafetyService(new HighConfidenceSecretDetector(), new ProvenancePolicy()),
            new DataProtectionSecretProtector(new EphemeralDataProtectionProvider()), new DepotAccessKeySecretHasher());
}
