using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Persistence;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Entities;
using Moq;

namespace ContextDepot.Application.Tests;

public sealed class BootstrapRetrievalTests
{
    private static readonly Guid OwnerId = Guid.Parse("0199c000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Explicit_scope_returns_only_current_workspace_and_respects_budget()
    {
        var workspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000010");
        var otherWorkspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000011");
        var workspace = new Workspace(workspaceId, OwnerId, null, "Portwise", "portwise", null, DateTimeOffset.UtcNow);
        var otherWorkspace = new Workspace(otherWorkspaceId, OwnerId, null, "Other", "other", null, DateTimeOffset.UtcNow);
        var matching = new ContextItem(Guid.CreateVersion7(), OwnerId, workspaceId, ContextKind.Decision, "project.database", null, "PostgreSQL is the database", DateTimeOffset.UtcNow);
        var unrelated = new ContextItem(Guid.CreateVersion7(), OwnerId, otherWorkspaceId, ContextKind.Decision, "project.database", null, "SQLite", DateTimeOffset.UtcNow);
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.GetWorkspacesAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([workspace, otherWorkspace]);
        repository.Setup(x => x.GetActiveContextsAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([matching, unrelated]);
        repository.Setup(x => x.GetIndexedDocumentChunksAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("portwise", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(workspaceId, OwnerId, "portwise", "Portwise", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var owner = new Mock<ICurrentOwnerContext>();
        owner.SetupGet(x => x.OwnerId).Returns(OwnerId);

        var result = await new ContextBootstrapAppService(owner.Object, workspaces.Object, repository.Object).BootstrapAsync(new BootstrapRequest("database", ["portwise"], 100), CancellationToken.None);

        var item = Assert.Single(result.Contexts);
        Assert.Equal(matching.Id, item.Id);
        Assert.Equal(ScopeResolutionStatus.Resolved, result.ScopeResolution.Status);
        Assert.Equal("lexical", result.Diagnostics.Mode);
    }

    [Fact]
    public async Task Close_auto_scope_candidates_remain_ambiguous()
    {
        var first = new Workspace(Guid.CreateVersion7(), OwnerId, null, "Travel", "travel", null, DateTimeOffset.UtcNow);
        var second = new Workspace(Guid.CreateVersion7(), OwnerId, null, "Travel Japan", "travel-japan", null, DateTimeOffset.UtcNow);
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.GetWorkspacesAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        repository.Setup(x => x.GetActiveContextsAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.GetIndexedDocumentChunksAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var owner = new Mock<ICurrentOwnerContext>();
        owner.SetupGet(x => x.OwnerId).Returns(OwnerId);

        var result = await new ContextBootstrapAppService(owner.Object, new Mock<IWorkspaceAppService>().Object, repository.Object).BootstrapAsync(new BootstrapRequest("travel"), CancellationToken.None);

        Assert.Equal(ScopeResolutionStatus.Ambiguous, result.ScopeResolution.Status);
        Assert.Equal(2, result.ScopeResolution.Workspaces.Count);
    }
}
