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
            r.Limit == Math.Min(50, max) && !r.IncludeDescendants &&
            (string.IsNullOrEmpty(workspace) ? r.Workspaces == null : r.Workspaces!.SequenceEqual(new[] { workspace }))), cancellation.Token))
            .ReturnsAsync(new ContextSearchResult([], [], new(false, false, "lexical")));
        var result = await Create(max).SearchAsync("decision", workspace, cancellation.Token);
        Assert.Empty(result.Contexts);
        contexts.VerifyAll();
        documents.VerifyNoOtherCalls();
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
