using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Domain.Owners;
using Moq;

namespace ContextDepot.Application.Tests.Bootstrap;

public sealed class BootstrapRetrievalTests
{
    private static readonly Guid OwnerId = Guid.Parse("0199c000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Explicit_scope_returns_only_current_workspace_and_respects_budget()
    {
        var workspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000010");
        var otherWorkspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000011");
        var workspace = new BootstrapWorkspaceCandidate(workspaceId, OwnerId, null, "Portwise", "portwise");
        var otherWorkspace = new BootstrapWorkspaceCandidate(otherWorkspaceId, OwnerId, null, "Other", "other");
        var matching = Context(workspaceId, "PostgreSQL is the database", "project.database");
        var unrelated = Context(otherWorkspaceId, "SQLite", "project.database");
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([workspace, otherWorkspace]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([matching, unrelated]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("portwise", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(workspaceId, OwnerId, "portwise", "Portwise", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var owner = Owner();

        var result = await CreateService(owner, workspaces, repository).BootstrapAsync(new BootstrapRequest("database", ["portwise"], 100), CancellationToken.None);

        var item = Assert.Single(result.Contexts);
        Assert.Equal(matching.Id, item.Id);
        Assert.Equal(ScopeResolutionStatus.Resolved, result.ScopeResolution.Status);
        Assert.Equal("lexical", result.Diagnostics.Mode);
    }

    [Fact]
    public async Task Close_auto_scope_candidates_remain_ambiguous()
    {
        var first = new BootstrapWorkspaceCandidate(Guid.CreateVersion7(), OwnerId, null, "Travel", "travel");
        var second = new BootstrapWorkspaceCandidate(Guid.CreateVersion7(), OwnerId, null, "Travel Japan", "travel-japan");
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var owner = Owner();

        var result = await CreateService(owner, new Mock<IWorkspaceAppService>(), repository)
            .BootstrapAsync(new BootstrapRequest("travel"), CancellationToken.None);

        Assert.Equal(ScopeResolutionStatus.Ambiguous, result.ScopeResolution.Status);
        Assert.Equal(2, result.ScopeResolution.Workspaces.Count);
        Assert.Empty(result.Contexts);
        Assert.Empty(result.Documents);
    }

    [Fact]
    public async Task Invalid_budget_is_rejected_instead_of_clamped()
    {
        var repository = new Mock<IBootstrapRepository>();
        var owner = Owner();

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            CreateService(owner, new Mock<IWorkspaceAppService>(), repository)
                .BootstrapAsync(new BootstrapRequest("anything", MaxTokens: 0), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.ContextBudgetInvalid, exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Nested_workspace_scope_and_document_excerpt_use_full_path()
    {
        var parentId = Guid.CreateVersion7();
        var childId = Guid.CreateVersion7();
        var parent = new BootstrapWorkspaceCandidate(parentId, OwnerId, null, "Projects", "projects");
        var child = new BootstrapWorkspaceCandidate(childId, OwnerId, parentId, "Context Depot", "context-depot");
        var chunk = new BootstrapDocumentChunkCandidate(
            Guid.CreateVersion7(),
            OwnerId,
            Guid.CreateVersion7(),
            childId,
            0,
            "docs/README.md",
            "README",
            "Overview",
            "PostgreSQL",
            "hash",
            DateTimeOffset.UtcNow);
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([parent, child]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([chunk]);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("projects/context-depot", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(childId, OwnerId, "projects/context-depot", child.Name, null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var result = await CreateService(Owner(), workspaces, repository)
            .BootstrapAsync(new BootstrapRequest("postgresql", ["projects/context-depot"], 100), CancellationToken.None);

        Assert.Equal("projects/context-depot", Assert.Single(result.ScopeResolution.Workspaces));
        Assert.Equal("projects/context-depot/docs/README.md", Assert.Single(result.Documents).Path);
    }

    private static BootstrapContextCandidate Context(Guid workspaceId, string content, string? key) => new(
        Guid.CreateVersion7(),
        OwnerId,
        workspaceId,
        ContextKind.Decision,
        key,
        null,
        content,
        "[]",
        50,
        null,
        ContextStatus.Active,
        VerificationStatus.Unknown,
        ProvenanceTrust.Unknown,
        SourceType.Agent,
        null,
        null,
        null,
        null,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        "{}");

    private static Mock<ICurrentOwnerContext> Owner()
    {
        var owner = new Mock<ICurrentOwnerContext>();
        owner.SetupGet(x => x.OwnerId).Returns(OwnerId);
        return owner;
    }

    private static ContextBootstrapAppService CreateService(
        Mock<ICurrentOwnerContext> owner,
        Mock<IWorkspaceAppService> workspaces,
        Mock<IBootstrapRepository> repository) =>
        new(owner.Object, workspaces.Object, repository.Object, TimeProvider.System);
}
