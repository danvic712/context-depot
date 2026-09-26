using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.IndexRepair;

public sealed class IndexRepairAppServiceTests
{
    private static readonly Guid DepotId = Guid.CreateVersion7();
    private static readonly Guid WorkspaceId = Guid.CreateVersion7();

    [Fact]
    public async Task Unchanged_context_vector_skips_embedding_generation()
    {
        var context = ContextCandidate();
        var text = new ContextEmbeddingTextBuilder().Build(new(
            context.WorkspacePath,
            context.Kind,
            context.Key,
            context.Title,
            ["database"],
            context.Content));
        var hash = EmbeddingInputHash.Compute(text);
        var vectors = new Mock<IVectorIndexRepository>();
        vectors.Setup(x => x.GetContextInputHashesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [context.ContextItemId] = hash });
        var generator = CreateGenerator();
        var repair = CreateService(
            CreateRepository(contextPage: [context]),
            vectors,
            generator);

        var result = await repair.RepairAsync(DepotId, new(32, 1), CancellationToken.None);

        Assert.Equal(0, result.ContextVectorsCreatedOrUpdated);
        Assert.False(result.RetrievalDegraded);
        generator.Verify(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        vectors.Verify(x => x.UpsertContextVectorsAsync(It.IsAny<IReadOnlyList<ContextDepot.Application.VectorIndex.Dtos.ContextVectorIndexWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stale_context_vector_is_regenerated_and_upserted()
    {
        var context = ContextCandidate();
        var vectors = new Mock<IVectorIndexRepository>();
        vectors.Setup(x => x.GetContextInputHashesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        var generator = CreateGenerator();
        var repair = CreateService(
            CreateRepository(contextPage: [context]),
            vectors,
            generator);

        var result = await repair.RepairAsync(DepotId, new(32, 1), CancellationToken.None);

        Assert.Equal(1, result.ContextVectorsCreatedOrUpdated);
        vectors.Verify(x => x.UpsertContextVectorsAsync(
            It.Is<IReadOnlyList<ContextDepot.Application.VectorIndex.Dtos.ContextVectorIndexWrite>>(writes => writes.Count == 1 && writes[0].ContextItemId == context.ContextItemId),
            It.IsAny<CancellationToken>()), Times.Once);
        generator.Verify(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Generator_failure_degrades_repair_without_changing_source()
    {
        var context = ContextCandidate();
        var vectors = new Mock<IVectorIndexRepository>();
        vectors.Setup(x => x.GetContextInputHashesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        var generator = CreateGenerator(throwOnGenerate: true);
        var repository = CreateRepository(contextPage: [context]);
        var repair = CreateService(repository, vectors, generator);

        var result = await repair.RepairAsync(DepotId, new(32, 1), CancellationToken.None);

        Assert.True(result.RetrievalDegraded);
        Assert.Equal(0, result.ContextVectorsCreatedOrUpdated);
        vectors.Verify(x => x.UpsertContextVectorsAsync(It.IsAny<IReadOnlyList<ContextDepot.Application.VectorIndex.Dtos.ContextVectorIndexWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.MarkDocumentIndexFailedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Full_context_page_returns_cursor_until_the_next_cycle()
    {
        var first = ContextCandidate(Guid.CreateVersion7());
        var second = ContextCandidate(Guid.CreateVersion7());
        var vectors = new Mock<IVectorIndexRepository>();
        vectors.Setup(x => x.GetContextInputHashesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>
            {
                [first.ContextItemId] = "old",
                [second.ContextItemId] = "old"
            });
        var generator = CreateGenerator();
        var repair = CreateService(
            CreateRepository(contextPage: [first, second]),
            vectors,
            generator);

        var result = await repair.RepairAsync(DepotId, new(2, 1), CancellationToken.None);

        Assert.Equal(second.ContextItemId, result.NextContextAfterId);
        Assert.False(result.ContextScanWrapped);
    }

    [Fact]
    public async Task Missing_markdown_marks_document_failed_with_safe_error_code()
    {
        var document = new DocumentIndexRepairCandidate(
            Guid.CreateVersion7(),
            DepotId,
            WorkspaceId,
            "projects/context-depot",
            "docs/readme.md",
            "README");
        var repository = CreateRepository(documentCandidates: [document]);
        var markdown = new Mock<IMarkdownStore>();
        markdown.Setup(x => x.GetAsync(DepotId, "projects/context-depot/docs/readme.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarkdownDocument?)null);
        var repair = CreateService(repository, new Mock<IVectorIndexRepository>(), CreateGenerator(), markdown);

        var result = await repair.RepairAsync(DepotId, new(32, 1), CancellationToken.None);

        Assert.True(result.RetrievalDegraded);
        repository.Verify(x => x.MarkDocumentIndexFailedAsync(
            DepotId,
            document.DocumentId,
            ContextDepot.Application.Shared.Exceptions.ApplicationErrorCodes.MarkdownFileMissing,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pending_document_is_reconciled_from_canonical_markdown()
    {
        var document = new DocumentIndexRepairCandidate(
            Guid.CreateVersion7(),
            DepotId,
            WorkspaceId,
            "projects/context-depot",
            "docs/readme.md",
            "README");
        var repository = CreateRepository(documentCandidates: [document]);
        var markdown = new Mock<IMarkdownStore>();
        markdown.Setup(x => x.GetAsync(DepotId, "projects/context-depot/docs/readme.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarkdownDocument("projects/context-depot/docs/readme.md", "# README\n\nCanonical content", "hash"));
        var documentRepository = new Mock<IDocumentRepository>();
        documentRepository.Setup(x => x.GetByIdAsync(DepotId, document.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document(document.DocumentId, DepotId, WorkspaceId, document.Path, document.Title, DateTimeOffset.UtcNow));
        var repair = CreateService(repository, new Mock<IVectorIndexRepository>(), CreateGenerator(), markdown, documentRepository);

        var result = await repair.RepairAsync(DepotId, new(32, 1), CancellationToken.None);

        Assert.Equal(1, result.DocumentsReconciled);
        documentRepository.Verify(x => x.ReconcileIndexAsync(
            It.Is<DocumentIndexWrite>(write => write.DocumentId == document.DocumentId && write.Chunks.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stale_document_vector_is_regenerated_and_upserted()
    {
        var chunk = new DocumentEmbeddingRepairCandidate(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DepotId,
            WorkspaceId,
            "projects/context-depot",
            "docs/readme.md",
            "README",
            "README",
            "Canonical content");
        var vectors = new Mock<IVectorIndexRepository>();
        vectors.Setup(x => x.GetDocumentInputHashesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        var repair = CreateService(
            CreateRepository(documentPage: [chunk]),
            vectors,
            CreateGenerator());

        var result = await repair.RepairAsync(DepotId, new(32, 1), CancellationToken.None);

        Assert.Equal(1, result.DocumentVectorsCreatedOrUpdated);
        vectors.Verify(x => x.UpsertDocumentVectorsAsync(
            It.Is<IReadOnlyList<ContextDepot.Application.VectorIndex.Dtos.DocumentVectorIndexWrite>>(writes => writes.Count == 1 && writes[0].DocumentChunkId == chunk.DocumentChunkId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ContextEmbeddingRepairCandidate ContextCandidate(Guid? id = null) => new(
        id ?? Guid.CreateVersion7(),
        DepotId,
        WorkspaceId,
        "projects/context-depot",
        ContextKind.Decision,
        "database.provider",
        "Database provider",
        "[\"database\"]",
        "Use PostgreSQL.");

    private static Mock<IIndexRepairRepository> CreateRepository(
        IReadOnlyList<ContextEmbeddingRepairCandidate>? contextPage = null,
        IReadOnlyList<DocumentIndexRepairCandidate>? documentCandidates = null,
        IReadOnlyList<DocumentEmbeddingRepairCandidate>? documentPage = null)
    {
        var repository = new Mock<IIndexRepairRepository>();
        repository.Setup(x => x.FindDocumentIndexRepairCandidatesAsync(DepotId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentCandidates ?? []);
        repository.Setup(x => x.FindContextSourcePageAsync(DepotId, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextPage ?? []);
        repository.Setup(x => x.FindDocumentChunkSourcePageAsync(DepotId, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentPage ?? []);
        return repository;
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateGenerator(bool throwOnGenerate = false)
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        if (throwOnGenerate)
        {
            generator.Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("transient"));
        }
        else
        {
            generator.Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new[] { 1f, 0f, 0f })]));
        }

        return generator;
    }

    private static IndexRepairAppService CreateService(
        Mock<IIndexRepairRepository> repository,
        Mock<IVectorIndexRepository> vectors,
        Mock<IEmbeddingGenerator<string, Embedding<float>>> generator,
        Mock<IMarkdownStore>? markdown = null,
        Mock<IDocumentRepository>? documentRepository = null)
    {
        documentRepository ??= new Mock<IDocumentRepository>();
        documentRepository.Setup(x => x.GetByIdAsync(DepotId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid documentId, CancellationToken _) =>
            {
                var document = new Document(documentId, DepotId, WorkspaceId, "docs/readme.md", "README", DateTimeOffset.UtcNow);
                return document;
            });
        documentRepository.Setup(x => x.ReconcileIndexAsync(It.IsAny<DocumentIndexWrite>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentIndexWrite write, CancellationToken _) =>
                new DocumentReconcilePersistenceResult(null!, ContextDepot.Application.Documents.Enums.DocumentReconcilePersistenceOutcome.Reconciled));
        var idGenerator = new Mock<IIdGenerator>();
        idGenerator.Setup(x => x.NewId()).Returns(Guid.CreateVersion7());
        var embeddingService = new EmbeddingGeneratorService(
            CreateServices(generator),
            Options.Create(new EmbeddingOptions { Dimensions = 3 }),
            new HighConfidenceSecretDetector(),
            new EmbeddingResultValidator(),
            NullLogger<EmbeddingGeneratorService>.Instance);
        return new IndexRepairAppService(
            repository.Object,
            documentRepository.Object,
            markdown?.Object ?? new Mock<IMarkdownStore>().Object,
            new HeadingAwareMarkdownChunker(),
            new DocumentWriteCoordinator(),
            new Mock<ISourceSafetyService>().Object,
            idGenerator.Object,
            new VectorIndexRepairer(
                vectors.Object,
                embeddingService,
                new ContextEmbeddingTextBuilder(),
                new DocumentEmbeddingTextBuilder(),
                NullLogger<VectorIndexRepairer>.Instance),
            TimeProvider.System,
            NullLogger<IndexRepairAppService>.Instance);
    }

    private static IServiceProvider CreateServices(Mock<IEmbeddingGenerator<string, Embedding<float>>> generator)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>)))
            .Returns(generator.Object);
        return services.Object;
    }
}
