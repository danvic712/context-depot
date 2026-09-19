using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Application.Documents.Enums;
using Moq;

namespace ContextDepot.Application.Tests.Documents;

public sealed class DocumentAppServiceTests
{
    private static readonly Guid OwnerId = Guid.CreateVersion7();
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();

    [Fact]
    public async Task Upsert_reconciles_an_archived_document_as_active()
    {
        var document = new Document(Guid.CreateVersion7(), OwnerId, WorkspaceId, "docs/readme.md", "Old title", DateTimeOffset.UtcNow);
        document.Reconcile("Old title", Hash("canonical"), DateTimeOffset.UtcNow);
        document.MarkArchived(DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByPathAsync(OwnerId, WorkspaceId, "docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(document);
        repository.Setup(x => x.ReconcileIndexAsync(It.IsAny<DocumentIndexWrite>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentIndexWrite write, CancellationToken _) =>
            {
                document.Reconcile(write.Title, write.ContentHash, write.Now);
                return new DocumentReconcilePersistenceResult(document, DocumentReconcilePersistenceOutcome.Reactivated);
            });
        var service = CreateService(repository, new MarkdownDocument("docs/readme.md", "canonical", Hash("canonical")));

        var result = await service.UpsertAsync(new UpsertDocumentCommand("projects/context-depot", "docs/readme.md", "README", "canonical"), CancellationToken.None);

        Assert.Equal(DocumentStatus.Active, result.Status);
        Assert.Equal(DocumentIndexStatus.Indexed, result.IndexStatus);
        repository.Verify(x => x.MarkIndexPendingAsync(OwnerId, WorkspaceId, "docs/readme.md", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.ReconcileIndexAsync(It.IsAny<DocumentIndexWrite>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_rejects_a_stale_canonical_hash()
    {
        var document = new Document(Guid.CreateVersion7(), OwnerId, WorkspaceId, "docs/readme.md", "README", DateTimeOffset.UtcNow);
        document.Reconcile("README", Hash("server"), DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByPathAsync(OwnerId, WorkspaceId, "docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(document);
        var service = CreateService(repository, new MarkdownDocument("docs/readme.md", "server", Hash("server")));

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.UpsertAsync(new UpsertDocumentCommand("projects/context-depot", "docs/readme.md", "README", "client", "different-hash"), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.DocumentConflict, exception.ErrorCode);
        repository.Verify(x => x.MarkIndexPendingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DocumentAppService CreateService(Mock<IDocumentRepository> repository, MarkdownDocument canonical)
    {
        var owner = new Mock<ICurrentOwnerContext>();
        owner.SetupGet(x => x.OwnerId).Returns(OwnerId);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("projects/context-depot", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(WorkspaceId, OwnerId, "projects/context-depot", "ContextDepot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        workspaces.Setup(x => x.GetAsync(WorkspaceId, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(WorkspaceId, OwnerId, "projects/context-depot", "ContextDepot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var markdown = new Mock<IMarkdownStore>();
        markdown.Setup(x => x.GetAsync("projects/context-depot/docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(canonical);
        var chunker = new HeadingAwareMarkdownChunker();
        var safety = new Mock<ISourceSafetyService>();
        var ids = new Mock<IIdGenerator>();
        ids.Setup(x => x.NewId()).Returns(Guid.CreateVersion7());
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.Parse("2026-09-19T00:00:00Z"));
        return new DocumentAppService(owner.Object, workspaces.Object, repository.Object, markdown.Object, chunker, new DocumentWriteCoordinator(), safety.Object, ids.Object, timeProvider.Object);
    }

    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
