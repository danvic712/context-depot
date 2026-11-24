using System.Text.Json;
using ContextDepot.Application;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Infrastructure.AgentSessions;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContextDepot.Infrastructure.Tests;

[Collection(CapturePostgreSqlCollection.Name)]
public sealed class SessionWorkspacePreparationTests(CapturePostgreSqlFixture fixture)
{
    private const string DurableStatement = "The SampleProject uses PostgreSQL for its persistent data.";

    [CapturePostgreSqlFact]
    public Task Native_message_pair_creates_a_child_under_an_authorized_parent() => VerifyCreationAsync(createRoot: false);

    [CapturePostgreSqlFact]
    public Task Native_message_pair_creates_a_root_with_creation_permission_and_no_prior_grants() => VerifyCreationAsync(createRoot: true);

    [CapturePostgreSqlFact]
    public async Task Revoking_root_creation_before_routing_prevents_workspace_creation()
    {
        var seed = await fixture.SeedAsync(canCreateRootWorkspaces: true, grantWorkspace: false);
        var locator = await fixture.AcceptPairAsync(seed);
        await using var db = fixture.CreateDb();
        var key = await db.DepotAccessKeys.SingleAsync();
        key.ChangeCreationCapability(false);
        await db.SaveChangesAsync();

        Assert.Null(await fixture.Inbox(db).TryClaimRouteAsync(locator, default));
        Assert.Equal("skipped_unauthorized", await db.SessionCaptureTurnRoutes.Select(route => route.ResultCode).SingleAsync());
        Assert.Single(await db.Workspaces.ToArrayAsync());
        Assert.Empty(await db.WorkspaceAccessGrants.ToArrayAsync());
    }

    private async Task VerifyCreationAsync(bool createRoot)
    {
        var seed = await fixture.SeedAsync(canCreateRootWorkspaces: createRoot, grantWorkspace: !createRoot);
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:ContextDepot"] = Environment.GetEnvironmentVariable(CapturePostgreSqlFixture.ConnectionVariable);
        configuration["ContextDepot:Root"] = Path.GetTempPath();
        configuration["AgentSessions:Enabled"] = "true";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContextDepotApplication(configuration);
        services.AddContextDepotInfrastructure(configuration);
        services.AddSingleton<ISessionContentProtector>(fixture.Protector);
        services.AddSingleton<ICaptureEvidenceProtector>(fixture.EvidenceProtector);
        services.AddSingleton<IChatClient>(new RoutingChatClient(createRoot));
        var extractor = new TestExtractor();
        services.AddSingleton<IMemoryExtractionService>(extractor);
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<InferenceRuntimeSnapshotAccessor>().Publish(new(null, InferenceRuntimeState.Unconfigured, null));
        var worker = provider.GetRequiredService<SessionCaptureCycleRunner>();

        async Task<SessionCaptureReceipt> SubmitAsync(string turn, string role, string text)
        {
            await using var scope = provider.CreateAsyncScope();
            Guid[] grants = createRoot ? [] : [seed.WorkspaceId];
            scope.ServiceProvider.GetRequiredService<CurrentDepotAccessContext>().Initialize(new(seed.KeyId,
                seed.DepotId, "Capture test", grants, grants, CanCreateRootWorkspaces: createRoot));
            return await scope.ServiceProvider.GetRequiredService<ISessionCaptureAppService>().CaptureAsync(new(2,
                "codex", "session", turn, null, $"codex:{turn}:{role}", null, null, "message", [new(role, text)],
                WorkspaceMode: "auto"), default);
        }

        var user = await SubmitAsync("turn-1", "user", DurableStatement);
        await worker.RunAsync(default);
        Assert.Equal(0, extractor.Invocations);
        await using var db = fixture.CreateDb();
        Assert.Empty(await fixture.Inbox(db).FindRoutingCandidatesAsync(64, default));
        Assert.Equal(1, await db.Workspaces.CountAsync());
        var status = new SessionCaptureStatusRepository(db);
        Assert.NotNull(await status.GetAsync(seed.DepotId, seed.KeyId, new(null, user.TurnReceiptId), default));

        var assistant = await SubmitAsync("turn-1", "assistant", "Understood.");
        Assert.Equal(user.TurnReceiptId, assistant.TurnReceiptId);
        await worker.RunAsync(default);
        await worker.RunAsync(default);
        var completed = await status.GetAsync(seed.DepotId, seed.KeyId, new(null, user.TurnReceiptId), default);
        Assert.True(completed?.ResultCode == "saved",
            $"Expected a saved turn, got {completed?.RoutingState}/{completed?.ProcessingState}: {completed?.ResultCode ?? completed?.LastErrorCode}");
        Assert.Equal(createRoot ? "sample-project" : "storage/sample-project", completed!.Workspace);
        var project = await db.Workspaces.SingleAsync(item => item.Slug == "sample-project");
        Assert.Equal(createRoot ? null : seed.WorkspaceId, project.ParentWorkspaceId);
        Assert.Equal("sample-project", (await db.WorkspaceSubjectBindings.SingleAsync()).SubjectKey);
        Assert.True(await db.WorkspaceAccessGrants.AnyAsync(grant => grant.DepotAccessKeyId == seed.KeyId && grant.WorkspaceId == project.Id));
        Assert.Equal(project.Id, (await db.ContextItems.SingleAsync()).WorkspaceId);
        Assert.True(await db.ContextCaptureEvidence.AnyAsync(item => item.WorkspaceId == project.Id));
        Assert.Equal(1, extractor.Invocations);

        await SubmitAsync("turn-2", "user", DurableStatement);
        await SubmitAsync("turn-2", "assistant", "Understood.");
        await worker.RunAsync(default);
        await worker.RunAsync(default);
        Assert.Equal(2, await db.Workspaces.CountAsync());
        Assert.Equal(1, await db.ContextItems.CountAsync());

        await SubmitAsync("turn-3", "user", "What does OtherProject use for persistent data?");
        await SubmitAsync("turn-3", "assistant", "I do not know.");
        await worker.RunAsync(default);
        Assert.Equal("skipped_no_new_memory", await db.SessionCaptureTurnRoutes.Where(route => route.TurnId == "turn-3")
            .Select(route => route.ResultCode).SingleAsync());
        Assert.Equal(2, await db.Workspaces.CountAsync());
    }

    private sealed class RoutingChatClient(bool createRoot) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            using var payload = JsonDocument.Parse(messages.Last(message => message.Role == ChatRole.User).Text);
            var user = payload.RootElement.GetProperty("user").GetString()!;
            var name = user.Contains("SampleProject", StringComparison.Ordinal) ? "SampleProject" : "OtherProject";
            var parents = payload.RootElement.GetProperty("parents").EnumerateArray().ToArray();
            if (createRoot) Assert.DoesNotContain(parents, parent => parent.GetProperty("path").GetString() == "storage");
            var parentId = createRoot ? "root" : parents.Single(parent => parent.GetProperty("path").GetString() == "storage")
                .GetProperty("parentId").GetString();
            var response = $$"""{"decision":"prepare","parentId":"{{parentId}}","projectName":"{{name}}"}""";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class TestExtractor : IMemoryExtractionService
    {
        public int Invocations { get; private set; }

        public Task<IReadOnlyList<MemoryCandidate>> ExtractAsync(string user, string assistant, CancellationToken cancellationToken)
        {
            Invocations++;
            IReadOnlyList<MemoryCandidate> candidates = user == DurableStatement
                ? [new("fact", "sample-project.database", Summary: DurableStatement, Quotes: [DurableStatement])]
                : [];
            return Task.FromResult(candidates);
        }
    }
}
