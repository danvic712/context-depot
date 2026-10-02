using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Overview;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Contexts.Enums;
using MatchType = ContextDepot.Application.Retrieval.Enums.MatchType;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.Overview;

public sealed class KnowledgeSearchAppServiceTests
{
    private readonly Mock<IContextQueryAppService> contexts = new();
    private readonly Mock<IDocumentAppService> documents = new();
    private readonly Mock<IWorkspaceAppService> workspaces = new();
    private KnowledgeSearchAppService Create(int max = 50)
    {
        var options = new Mock<IOptionsMonitor<RetrievalOptions>>();
        options.SetupGet(x => x.CurrentValue).Returns(new RetrievalOptions { Search = new() { MaxLimit = max } });
        return new(contexts.Object, documents.Object, workspaces.Object, options.Object);
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData("projects/notes", 15)]
    [InlineData("", 100)]
    public async Task SearchDelegatesToScopedRetrievalAndHonorsConfiguredLimit(string? workspace, int max)
    {
        using var cancellation = new CancellationTokenSource();
        contexts.Setup(x => x.SearchAsync(It.Is<ContextSearchRequest>(r => r.Query == "decision" &&
            r.Limit == Math.Min(50, max) && !r.IncludeDescendants && r.IncludeDocuments && r.IncludeResultOrder &&
            (string.IsNullOrEmpty(workspace) ? r.Workspaces == null : r.Workspaces!.SequenceEqual(new[] { workspace }))), cancellation.Token))
            .ReturnsAsync(new ContextSearchResult([], [], new(false, false, "lexical")));
        var result = await Create(max).SearchAsync("decision", workspace, cancellation.Token);
        Assert.Empty(result.Items);
        Assert.Equal(Math.Min(50, max), result.Limit);
        contexts.VerifyAll();
        documents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchPreservesMixedRankOrderAndDeduplicatesDocumentChunks()
    {
        var id = Guid.NewGuid();
        contexts.Setup(x => x.SearchAsync(It.IsAny<ContextSearchRequest>(), default)).ReturnsAsync(
            new ContextSearchResult(
                [new(id, "projects", ContextKind.Decision, "choice", null, "Decision", MatchType.Lexical, 1)],
                [new(id, "projects", "design.md", "Design", "", "Best chunk", MatchType.Lexical, 0),
                 new(id, "projects", "design.md", "Design", "", "Other chunk", MatchType.Lexical, 2)],
                new(true, false, "lexical-degraded")));
        var result = await Create().SearchAsync("decision", null, default);
        Assert.Equal(new[] { "document", "context" }, result.Items.Select(x => x.Type));
        Assert.Equal("Best chunk", result.Items[0].Excerpt);
        Assert.Equal("choice", result.Items[1].Title);
        Assert.True(result.Degraded);
    }

    [Theory]
    [InlineData("fact", ContextKind.Fact)]
    [InlineData("preference", ContextKind.Preference)]
    [InlineData("decision", ContextKind.Decision)]
    [InlineData("goal", ContextKind.Goal)]
    [InlineData("state", ContextKind.State)]
    [InlineData("event", ContextKind.Event)]
    [InlineData("observation", ContextKind.Observation)]
    public async Task KindFiltersBeforeRetrievalAndExcludesDocuments(string kind, ContextKind expected)
    {
        contexts.Setup(x => x.SearchAsync(It.Is<ContextSearchRequest>(r => !r.IncludeDocuments &&
            r.IncludeResultOrder && r.Kinds!.SequenceEqual(new[] { expected })), default))
            .ReturnsAsync(new ContextSearchResult([], [], new(false, false, "lexical")));
        await Create().SearchAsync("decision", null, default, kind);
        contexts.VerifyAll();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("fact,decision")]
    public async Task InvalidKindCannotSilentlySearchAllKnowledge(string kind)
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => Create().SearchAsync("query", null, default, kind));
        Assert.Equal(ApplicationErrorCodes.InvalidSearchQuery, error.ErrorCode);
        contexts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WorkspaceChoicesUseFullVisiblePathsIncludingNestedSpaces()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        workspaces.Setup(x => x.LoadTopologyAsync(default)).ReturnsAsync(new WorkspaceTopology([
            new(root, null, "projects"), new(child, root, "notes")]));
        var result = await Create().ListWorkspacesAsync(default);
        Assert.Equal(new[] { "projects", "projects/notes" }, result.Select(x => x.Path));
        Assert.Equal(child, result[1].Id);
    }

    [Theory]
    [InlineData("context", ApplicationErrorCodes.ContextNotFound)]
    [InlineData("document", ApplicationErrorCodes.DocumentNotFound)]
    [InlineData("unknown", ApplicationErrorCodes.InvalidResourceQuery)]
    public async Task InvalidOrMissingPreviewThrowsBusinessCode(string type, string code)
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => Create().GetAsync(type, Guid.NewGuid(), default));
        Assert.Equal(code, error.ErrorCode);
        Assert.Equal(code, error.Message);
    }

    [Fact]
    public async Task DocumentPreviewReadsCanonicalMarkdownInsteadOfSearchChunks()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var metadata = new DocumentModel(id, Guid.NewGuid(), Guid.NewGuid(), "projects/notes", "notes.md", "Notes", "hash", "hash", DocumentStatus.Active, DocumentIndexStatus.Indexed, now, now, []);
        documents.Setup(x => x.GetAsync(id, default)).ReturnsAsync(new DocumentContentModel(metadata, "# Complete Markdown\n\nNot a search excerpt.", "hash"));
        var result = await Create().GetAsync("document", id, default);
        Assert.Equal("# Complete Markdown\n\nNot a search excerpt.", result.Content);
        Assert.Equal(now, result.UpdatedAt);
        Assert.Equal("projects/notes", result.Workspace);
        contexts.VerifyNoOtherCalls();
    }
}
