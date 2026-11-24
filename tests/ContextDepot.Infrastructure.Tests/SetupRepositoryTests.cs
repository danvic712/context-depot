using ContextDepot.Application.Setup;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Infrastructure.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Setup.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Domain.Access;
using ContextDepot.Domain.Setup;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Access;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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
        Assert.Null(state.InitialWorkspaceId);
        Assert.Null(state.CompletedAt);
        Assert.False(state.InferenceReviewed);
        Assert.False(state.AccessKeyReviewed);
        Assert.Empty(await db.Workspaces.ToArrayAsync());

        db.InstallationStates.Add(new InstallationState(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgreSqlFact]
    public async Task RemovingDepotPreservesExistingWorkspacesAndCredentials()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync("20261123150203_AddSessionWorkspacePreparation");
        await using var db = fixture.CreateDb(true);
        var now = DateTimeOffset.UtcNow;
        var legacyId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();
        var childId = Guid.CreateVersion7();
        var keyId = Guid.CreateVersion7();
        var hasher = new AccessKeySecretHasher();
        var secret = hasher.Generate();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.depots (id, display_name, metadata, created_at, updated_at)
            VALUES ({legacyId}, 'Existing knowledge', {"{}"}::jsonb, {now}, {now});
            INSERT INTO public.workspaces (id, depot_id, parent_workspace_id, name, slug, metadata, created_at, updated_at)
            VALUES ({workspaceId}, {legacyId}, NULL, 'Project', 'project', {"{}"}::jsonb, {now}, {now}),
                   ({childId}, {legacyId}, {workspaceId}, 'Notes', 'notes', {"{}"}::jsonb, {now}, {now});
            INSERT INTO public.depot_access_keys (id, depot_id, name, key_prefix, secret_hash, created_at)
            VALUES ({keyId}, {legacyId}, 'Existing key', {secret.Prefix}, {secret.SecretHash}, {now});
            INSERT INTO public.workspace_access_grants (depot_access_key_id, depot_id, workspace_id, created_at)
            VALUES ({keyId}, {legacyId}, {childId}, {now});
            """);

        await db.Database.MigrateAsync();
        var workspace = await db.Workspaces.SingleAsync(item => item.Id == childId);
        Assert.Equal(workspaceId, workspace.ParentWorkspaceId);
        Assert.Equal($"{legacyId:N}/project/notes", workspace.StoragePath);
        var identity = await new AccessKeyAuthenticator(db, hasher, TimeProvider.System).AuthenticateAsync(secret.Plaintext);
        Assert.Equal(keyId, identity!.AccessKeyId);
        Assert.Equal([childId], identity.WorkspaceIds);
        Assert.Contains(workspaceId, identity.NavigableWorkspaceIds);
        Assert.Equal(keyId, (await db.AccessKeys.SingleAsync()).Id);
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>(
            """
            SELECT count(*) AS "Value" FROM information_schema.columns
            WHERE table_schema = split_part('public.workspaces', '.', 1)
                AND (table_name IN ('depots', 'depot_access_keys', 'users')
                    OR column_name LIKE '%depot_id'
                    OR (table_name = 'access_keys' AND column_name = 'user_id'))
            """).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task RemovingDepotRejectsDatabasesContainingSeparateLegacyDepots()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync("20261123150203_AddSessionWorkspacePreparation");
        await using var db = fixture.CreateDb(true);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO public.depots (id, display_name, metadata, created_at, updated_at)
            VALUES (gen_random_uuid(), 'First', jsonb_build_object(), now(), now()),
                   (gen_random_uuid(), 'Second', jsonb_build_object(), now(), now());
            """);
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.MigrateAsync());
        Assert.Contains("one legacy Depot", error.MessageText);
        Assert.Equal(2L, await db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM public.depots").SingleAsync());
    }

    [PostgreSqlFact]
    public async Task NoConfigurationIsWrittenUntilFinalConfirmationAndAllResourcesAreCommittedTogether()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb();
        var service = CreateService(db);
        Assert.Equal("pending", (await service.GetAsync(default)).State);
        var catalog = await service.GetInferenceSettingsAsync(default);
        Assert.Empty(await db.Workspaces.ToArrayAsync()); Assert.Empty(await db.AccessKeys.ToArrayAsync());
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
        Assert.Single(await verify.Workspaces.ToArrayAsync());
        var key = Assert.Single(await verify.AccessKeys.Include(key => key.WorkspaceGrants).ToArrayAsync());
        Assert.Equal(result.Status.Workspace!.Id, Assert.Single(key.WorkspaceGrants).WorkspaceId);
        Assert.Equal(2, await verify.InferenceRoutes.CountAsync(route => route.ProviderId == preset.Id));
        var saved = await verify.InferenceProviders.SingleAsync(item => item.Id == preset.Id);
        Assert.NotEqual("fixture-api-key", saved.ProtectedApiKey); Assert.Contains("embedding-model", (await verify.InferenceRoutes.SingleAsync(route => route.Capability == "embedding")).ModelName);
        var identity = await new AccessKeyAuthenticator(verify, new AccessKeySecretHasher(), TimeProvider.System).AuthenticateAsync(result.AccessKey.Secret);
        Assert.Equal(key.Id, identity!.AccessKeyId);
        var repeated = await service.CompleteAsync(request, default);
        Assert.Null(repeated.AccessKey); Assert.Equal(result.Status, repeated.Status);
        Assert.Equal(1, await verify.AccessKeys.CountAsync());
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
        Assert.Equal(1, await verify.Workspaces.CountAsync()); Assert.Equal(1, await verify.AccessKeys.CountAsync());
    }

    [PostgreSqlFact]
    public async Task FailedFinalWriteRollsBackSpaceModelsKeyAndInstallationAndAllowsRetryInSameScope()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync(); await using var db = fixture.CreateDb();
        var repository = new SetupRepository(db); var now = DateTimeOffset.UtcNow;
        var workspace = new Workspace(Guid.CreateVersion7(), null, "Failed", "failed", null, now);
        var hasher = new AccessKeySecretHasher(); var secret = hasher.Generate();
        var key = new AccessKey(Guid.CreateVersion7(), "Failed", secret.Prefix, secret.SecretHash, now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, Guid.CreateVersion7(), now));
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.MutateAsync(snapshot =>
        {
            var provider = snapshot.Providers.Single(provider => provider.Kind == "openai"); provider.ProtectedApiKey = "failed-fixture-ciphertext";
            var route = snapshot.Routes.Single(route => route.Capability == "embedding");
            route.Provider = provider; route.ProviderId = provider.Id; route.ModelName = "failed-model"; route.Dimensions = 8;
            snapshot.Installation.Begin(workspace.Id, now); snapshot.Installation.Complete(now);
            return new SetupResources(workspace) { Providers = [provider], Routes = snapshot.Routes, AccessKey = key };
        }, default));
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified);
        Assert.Empty(await db.Workspaces.ToArrayAsync()); Assert.Empty(await db.AccessKeys.ToArrayAsync());
        Assert.False(await db.InferenceProviders.AnyAsync(provider => provider.ProtectedApiKey != null));
        Assert.Equal("pending", (await repository.GetAsync(default)).Installation.State);
        Assert.False(await db.InferenceRoutes.AnyAsync(route => route.ProviderId != null));
        var result = await CreateService(db).CompleteAsync(new(new("Knowledge", "knowledge"), []), default);
        Assert.Equal("completed", result.Status.State); Assert.Single(await db.Workspaces.IgnoreQueryFilters().ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task LegacyPartialSetupCanBeEditedAtFinalConfirmationWithoutReplacingItsSpace()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync(); await using var db = fixture.CreateDb(true);
        var now = DateTimeOffset.UtcNow;
        var workspace = new Workspace(Guid.CreateVersion7(), null, "Old", "old", "Old", now);
        await new SetupRepository(db).MutateAsync(snapshot =>
        { snapshot.Installation.Begin(workspace.Id, now); return new SetupResources(workspace); }, default);
        var service = CreateService(db);
        Assert.Equal("workspace", (await service.GetAsync(default)).NextStep);
        var result = await service.CompleteAsync(new(new("Edited", "edited", "Updated"), []), default);
        Assert.Equal(workspace.Id, result.Status.Workspace!.Id); Assert.Equal("edited", result.Status.Workspace.Path);
        Assert.Equal(1, await db.Workspaces.CountAsync());
        db.ChangeTracker.Clear(); Assert.Equal("Updated", (await db.Workspaces.SingleAsync()).Description);
    }

    [PostgreSqlFact]
    public async Task DatabaseRejectsMissingInitialWorkspace()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync(); await using var db = fixture.CreateDb(true);
        var result = await CreateService(db).CompleteAsync(new(new("Knowledge", "knowledge"), []), default);
        await Assert.ThrowsAsync<DbUpdateException>(() => new SetupRepository(db).MutateAsync(snapshot =>
        { snapshot.Installation.Begin(Guid.CreateVersion7(), DateTimeOffset.UtcNow); return null; }, default));
        Assert.Equal(result.Status.Workspace!.Id, (await new SetupRepository(db).GetAsync(default)).Workspace!.Id);
    }

    private static SetupAppService CreateService(ContextDepotDbContext db) =>
        new(new SetupRepository(db), new GuidV7IdGenerator(), TimeProvider.System,
            new SourceSafetyService(new HighConfidenceSecretDetector(), new ProvenancePolicy()),
            new DataProtectionSecretProtector(new EphemeralDataProtectionProvider()), new AccessKeySecretHasher());
}
