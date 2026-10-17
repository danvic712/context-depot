using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Documents;
using Moq;

namespace ContextDepot.Application.Tests.Documents;

public sealed class DocumentManagementAppServiceTests
{
    private readonly Guid depotId = Guid.CreateVersion7();
    private readonly Guid spaceId = Guid.CreateVersion7();
    private readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-15T00:00:00Z");
    private readonly Mock<IDocumentRepository> documents = new();
    private readonly Mock<IDocumentManagementRepository> archived = new();
    private readonly Mock<IWorkspaceAppService> workspaces = new();

    private DocumentManagementAppService Service(bool management = true)
    {
        var depot = new Mock<ICurrentDepotContext>(); depot.SetupGet(x => x.DepotId).Returns(depotId);
        var access = new Mock<IWorkspaceAccessContext>(); access.SetupGet(x => x.HasUnrestrictedAccess).Returns(management);
        if (!management) access.SetupGet(x => x.DepotAccessKeyId).Returns(Guid.CreateVersion7());
        workspaces.Setup(x => x.GetAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new WorkspaceModel(spaceId, depotId, "notes", "Notes", null, "{}", now, now));
        return new(depot.Object, access.Object, workspaces.Object, documents.Object, archived.Object, new DocumentWriteCoordinator());
    }

    [Fact]
    public async Task Permanent_deletion_requires_trusted_web_management_before_reading_or_writing()
    {
        var service = Service(false);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.DeleteAsync(Guid.CreateVersion7(), now, default));
        Assert.Equal(ApplicationErrorCodes.SettingsForbidden, error.ErrorCode);
        documents.VerifyNoOtherCalls(); archived.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Listing_validates_pagination_and_workspace_scope_before_querying_documents()
    {
        var service = Service();
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.ListArchivedAsync(spaceId, new PaginationQuery { PageSize = 61 }, default));
        var hiddenId = Guid.CreateVersion7();
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.ListArchivedAsync(hiddenId, new PaginationQuery(), default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceNotFound, error.ErrorCode);
        archived.VerifyNoOtherCalls();
        var result = new ArchivedDocumentPage([], 0, 1, 12);
        archived.Setup(x => x.ListArchivedAsync(depotId, spaceId, It.IsAny<PaginationQuery>(), default)).ReturnsAsync(result);
        Assert.Same(result, await service.ListArchivedAsync(spaceId, new PaginationQuery(), default));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Changed_documents_are_never_deleted(bool isArchived, bool stale)
    {
        var document = new Document(Guid.CreateVersion7(), depotId, spaceId, "old.md", "Old", now);
        if (isArchived) document.MarkArchived(now);
        documents.Setup(x => x.GetByIdAsync(depotId, document.Id, default)).ReturnsAsync(document);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => Service().DeleteAsync(document.Id, stale ? now.AddSeconds(-1) : now, default));
        Assert.Equal(ApplicationErrorCodes.DocumentConflict, error.ErrorCode);
        archived.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deletion_uses_server_resolved_canonical_path_for_active_and_archived_documents(bool isArchived)
    {
        var document = new Document(Guid.CreateVersion7(), depotId, spaceId, "docs/old.md", "Old", now);
        if (isArchived) document.MarkArchived(now);
        documents.Setup(x => x.GetByIdAsync(depotId, document.Id, default)).ReturnsAsync(document);
        archived.Setup(x => x.DeleteAsync(depotId, document.Id, spaceId, "notes/docs/old.md", now, default)).Returns(Task.CompletedTask);
        await Service().DeleteAsync(document.Id, now, default);
        archived.VerifyAll();
    }
}
