using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Documents;
using Moq;

namespace ContextDepot.Application.Tests.Documents;

public sealed class DocumentLinkTests
{
    [Theory]
    [InlineData("guide.md", "docs/guide.md")]
    [InlineData("./guide.md", "docs/guide.md")]
    [InlineData("../reference/guide.md", "reference/guide.md")]
    [InlineData("/reference/guide.md", "reference/guide.md")]
    [InlineData("%E4%B8%AD%E6%96%87%20%E6%96%87%E6%A1%A3.md", "docs/中文 文档.md")]
    [InlineData("../docs/guide.MD", "docs/guide.MD")]
    [InlineData("guide%23section.md", "docs/guide#section.md")]
    [InlineData("guide%3Fnotes.md", "docs/guide?notes.md")]
    public void Resolves_relative_and_workspace_root_links(string link, string expected) =>
        Assert.Equal(expected, DocumentPath.ResolveLink("docs/source.md", link));

    [Theory]
    [InlineData("../../outside.md")]
    [InlineData("%2e%2e/%2e%2e/outside.md")]
    [InlineData("//example.com/guide.md")]
    [InlineData("https://example.com/guide.md")]
    [InlineData("file:///guide.md")]
    [InlineData("..\\outside.md")]
    [InlineData("guide.md%00")]
    [InlineData("guide.md#heading")]
    [InlineData("guide.md?query=1")]
    [InlineData("guide.txt")]
    [InlineData("")]
    public void Rejects_invalid_paths_and_workspace_escape(string link)
    {
        var error = Assert.Throws<ContextDepotApplicationException>(() => DocumentPath.ResolveLink("docs/source.md", link));
        Assert.Equal(ApplicationErrorCodes.InvalidDocumentPath, error.ErrorCode);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("missing-source")]
    [InlineData("archived-source")]
    [InlineData("missing-workspace")]
    [InlineData("wrong-workspace")]
    [InlineData("missing-target")]
    [InlineData("archived-target")]
    public async Task Resolution_requires_an_active_source_and_target_in_the_authorized_workspace(string scenario)
    {
        var depotId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var source = new Document(Guid.NewGuid(), depotId, workspaceId, "docs/source.md", "Source", DateTimeOffset.UtcNow);
        var target = new Document(Guid.NewGuid(), depotId, workspaceId, "guide.md", "Target", DateTimeOffset.UtcNow);
        if (scenario == "archived-source") source.MarkArchived(DateTimeOffset.UtcNow);
        if (scenario == "archived-target") target.MarkArchived(DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByIdAsync(depotId, source.Id, default))
            .ReturnsAsync(scenario == "missing-source" ? null : source);
        repository.Setup(x => x.GetByPathAsync(depotId, workspaceId, "guide.md", default))
            .ReturnsAsync(scenario == "missing-target" ? null : target);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.GetAsync(workspaceId, default)).ReturnsAsync(scenario == "missing-workspace" ? null :
            new WorkspaceModel(workspaceId, depotId, "project", "Project", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(depotId);
        var service = new DocumentAppService(depot.Object, workspaces.Object, repository.Object,
            Mock.Of<IMarkdownStore>(), new HeadingAwareMarkdownChunker(), new DocumentWriteCoordinator(),
            Mock.Of<ISourceSafetyService>(), Mock.Of<IIdGenerator>(), TimeProvider.System);
        var constraint = scenario == "wrong-workspace" ? Guid.NewGuid() : workspaceId;
        if (scenario == "active")
            Assert.Equal(target.Id, await service.ResolveLinkAsync(source.Id, "../guide.md", default, constraint));
        else
        {
            var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
                service.ResolveLinkAsync(source.Id, "../guide.md", default, constraint));
            Assert.Equal(ApplicationErrorCodes.DocumentNotFound, error.ErrorCode);
        }
        if (scenario is "missing-source" or "archived-source" or "missing-workspace" or "wrong-workspace")
            repository.Verify(x => x.GetByPathAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
    }
}
