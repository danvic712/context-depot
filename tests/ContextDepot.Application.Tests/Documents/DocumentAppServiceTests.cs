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
    private static readonly Guid DepotId = Guid.CreateVersion7();
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();

    [Fact]
    public async Task Upsert_reconciles_an_archived_document_as_active()
    {
        var document = new Document(Guid.CreateVersion7(), DepotId, WorkspaceId, "docs/readme.md", "Old title", DateTimeOffset.UtcNow);
        document.Reconcile("Old title", Hash("canonical"), DateTimeOffset.UtcNow);
        document.MarkArchived(DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(document);
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
        repository.Verify(x => x.MarkIndexPendingAsync(DepotId, WorkspaceId, "docs/readme.md", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.ReconcileIndexAsync(It.IsAny<DocumentIndexWrite>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_rejects_a_stale_canonical_hash()
    {
        var document = new Document(Guid.CreateVersion7(), DepotId, WorkspaceId, "docs/readme.md", "README", DateTimeOffset.UtcNow);
        document.Reconcile("README", Hash("server"), DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(document);
        var service = CreateService(repository, new MarkdownDocument("docs/readme.md", "server", Hash("server")));

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.UpsertAsync(new UpsertDocumentCommand("projects/context-depot", "docs/readme.md", "README", "client", "different-hash"), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.DocumentConflict, exception.ErrorCode);
        repository.Verify(x => x.MarkIndexPendingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Identical_upsert_preserves_chunks_without_rebuilding_the_index()
    {
        var now = DateTimeOffset.Parse("2026-09-19T00:00:00Z");
        var document = new Document(Guid.CreateVersion7(), DepotId, WorkspaceId, "docs/readme.md", "README", now);
        document.Reconcile("README", Hash("canonical"), now);
        var chunk = new DocumentChunk(Guid.CreateVersion7(), DepotId, document.Id, WorkspaceId, 0, "", "canonical", Hash("canonical"), now);
        document.Chunks.Add(chunk);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(document);
        var result = await CreateService(repository, new MarkdownDocument("docs/readme.md", "canonical", Hash("canonical")))
            .UpsertAsync(new UpsertDocumentCommand("projects/context-depot", "docs/readme.md", " README ", "canonical"), default);
        Assert.Equal(chunk.Id, Assert.Single(result.Chunks).Id);
        Assert.Equal(now, result.UpdatedAt);
        repository.Verify(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/readme.md", It.IsAny<CancellationToken>()), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_links_cannot_change_canonical_content_or_mark_an_index_pending(bool existing)
    {
        const string invalid = "# Source\n\n[Bad](../../outside.md)";
        var source = new Document(Guid.NewGuid(), DepotId, WorkspaceId, "docs/readme.md", "README", DateTimeOffset.UtcNow);
        source.Reconcile("README", Hash(invalid), DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        if (existing) repository.Setup(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/readme.md", default)).ReturnsAsync(source);
        var markdown = new Mock<IMarkdownStore>();
        var service = CreateService(repository, existing ? new MarkdownDocument("docs/readme.md", invalid, Hash(invalid)) : null, markdown);

        var error = await Assert.ThrowsAsync<DocumentLinkValidationException>(() =>
            service.UpsertAsync(new("projects/context-depot", "docs/readme.md", "README", invalid), default));

        Assert.Equal(ApplicationErrorCodes.InvalidDocumentLinks, error.ErrorCode);
        Assert.Equal("invalid_path", Assert.Single(error.LinkErrors).Code);
        Assert.Equal(3, error.LinkErrors[0].Line);
        markdown.Verify(x => x.WriteAtomicAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        repository.Verify(x => x.MarkIndexPendingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), default), Times.Never);
        repository.Verify(x => x.ReconcileIndexAsync(It.IsAny<DocumentIndexWrite>(), default), Times.Never);
    }

    [Fact]
    public async Task Forward_references_save_with_warnings_and_get_rechecks_after_the_target_is_saved()
    {
        const string content = "# Source\n\n[Target](target.md#ready)";
        var repository = new Mock<IDocumentRepository>();
        var markdown = new Mock<IMarkdownStore>();
        var service = CreateService(repository, null, markdown);
        MarkdownDocument? canonical = null;
        Document? source = null;
        markdown.Setup(x => x.GetAsync(DepotId, "projects/context-depot/docs/readme.md", default)).ReturnsAsync(() => canonical);
        markdown.Setup(x => x.WriteAtomicAsync(DepotId, "projects/context-depot/docs/readme.md", content, default))
            .Callback(() => canonical = new("docs/readme.md", content, Hash(content))).Returns(Task.CompletedTask);
        repository.Setup(x => x.ReconcileIndexAsync(It.IsAny<DocumentIndexWrite>(), default))
            .ReturnsAsync((DocumentIndexWrite write, CancellationToken _) =>
            {
                source = new(write.DocumentId, DepotId, WorkspaceId, write.Path, write.Title, write.Now);
                source.Reconcile(write.Title, write.ContentHash, write.Now);
                return new DocumentReconcilePersistenceResult(source, DocumentReconcilePersistenceOutcome.Created);
            });

        var saved = await service.UpsertAsync(new("projects/context-depot", "docs/readme.md", "README", content), default);
        Assert.True(saved.LinkValidation.IsValid);
        Assert.Equal("target_not_found", Assert.Single(saved.LinkValidation.Warnings).Code);
        Assert.Equal(content, canonical!.Content);
        repository.Setup(x => x.GetByIdAsync(DepotId, saved.Id, default)).ReturnsAsync(() => source);
        var target = new Document(Guid.NewGuid(), DepotId, WorkspaceId, "docs/target.md", "Target", DateTimeOffset.UtcNow);
        repository.Setup(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/target.md", default)).ReturnsAsync(target);
        markdown.Setup(x => x.GetAsync(DepotId, "projects/context-depot/docs/target.md", default))
            .ReturnsAsync(new MarkdownDocument("docs/target.md", "# Ready", Hash("# Ready")));

        var readback = await service.GetAsync(saved.Id, default);
        Assert.NotNull(readback);
        Assert.Equal(saved.ContentHash, readback.ContentHash);
        Assert.Empty(readback.Metadata.LinkValidation.Errors);
        Assert.Empty(readback.Metadata.LinkValidation.Warnings);
        markdown.Verify(x => x.WriteAtomicAsync(DepotId, "projects/context-depot/docs/readme.md", content, default), Times.Once);
    }

    [Fact]
    public async Task Unchanged_upserts_revalidate_target_state_without_rebuilding_the_index()
    {
        const string content = "[Missing](missing.md)";
        var source = new Document(Guid.NewGuid(), DepotId, WorkspaceId, "docs/readme.md", "README", DateTimeOffset.UtcNow);
        source.Reconcile("README", Hash(content), DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByPathAsync(DepotId, WorkspaceId, "docs/readme.md", default)).ReturnsAsync(source);
        var service = CreateService(repository, new("docs/readme.md", content, Hash(content)));
        var result = await service.UpsertAsync(new("projects/context-depot", "docs/readme.md", "README", content), default);
        Assert.Equal("target_not_found", Assert.Single(result.LinkValidation.Warnings).Code);
        repository.Verify(x => x.MarkIndexPendingAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), default), Times.Never);
    }

    [Fact]
    public async Task Reading_an_existing_invalid_document_reports_errors_without_blocking_its_content()
    {
        const string content = "[Old link](../../outside.md)";
        var source = new Document(Guid.NewGuid(), DepotId, WorkspaceId, "docs/readme.md", "README", DateTimeOffset.UtcNow);
        var repository = new Mock<IDocumentRepository>();
        repository.Setup(x => x.GetByIdAsync(DepotId, source.Id, default)).ReturnsAsync(source);
        var service = CreateService(repository, new("docs/readme.md", content, Hash(content)));
        var result = await service.GetAsync(source.Id, default);
        Assert.NotNull(result);
        Assert.Equal(content, result.Content);
        Assert.False(result.Metadata.LinkValidation.IsValid);
        Assert.Equal("invalid_path", Assert.Single(result.Metadata.LinkValidation.Errors).Code);
    }

    private static DocumentAppService CreateService(Mock<IDocumentRepository> repository, MarkdownDocument? canonical,
        Mock<IMarkdownStore>? markdownStore = null)
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(DepotId);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("projects/context-depot", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(WorkspaceId, DepotId, "projects/context-depot", "ContextDepot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        workspaces.Setup(x => x.GetAsync(WorkspaceId, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(WorkspaceId, DepotId, "projects/context-depot", "ContextDepot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var markdown = markdownStore ?? new Mock<IMarkdownStore>();
        markdown.Setup(x => x.GetAsync(DepotId, "projects/context-depot/docs/readme.md", It.IsAny<CancellationToken>())).ReturnsAsync(canonical);
        var chunker = new HeadingAwareMarkdownChunker();
        var safety = new Mock<ISourceSafetyService>();
        var ids = new Mock<IIdGenerator>();
        ids.Setup(x => x.NewId()).Returns(Guid.CreateVersion7());
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.Parse("2026-09-19T00:00:00Z"));
        return new DocumentAppService(depot.Object, workspaces.Object, repository.Object, markdown.Object, chunker, new DocumentWriteCoordinator(), safety.Object, ids.Object, timeProvider.Object);
    }

    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
