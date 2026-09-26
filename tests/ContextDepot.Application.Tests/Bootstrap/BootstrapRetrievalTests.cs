using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Domain.Depots;
using Microsoft.Extensions.AI;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Tests.Bootstrap;

public sealed class BootstrapRetrievalTests
{
    private static readonly Guid DepotId = Guid.Parse("0199c000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Explicit_scope_returns_only_current_workspace_and_respects_budget()
    {
        var workspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000010");
        var otherWorkspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000011");
        var workspace = new BootstrapWorkspaceCandidate(workspaceId, DepotId, null, "Portwise", "portwise");
        var otherWorkspace = new BootstrapWorkspaceCandidate(otherWorkspaceId, DepotId, null, "Other", "other");
        var matching = Context(workspaceId, "PostgreSQL is the database", "project.database");
        var unrelated = Context(otherWorkspaceId, "SQLite", "project.database");
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([workspace, otherWorkspace]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([matching, unrelated]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("portwise", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(workspaceId, DepotId, "portwise", "Portwise", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var depot = Depot();

        var result = await CreateService(depot, workspaces, repository).BootstrapAsync(new BootstrapRequest("database", ["portwise"], 100), CancellationToken.None);

        var item = Assert.Single(result.Contexts);
        Assert.Equal(matching.Id, item.Id);
        Assert.Equal(ScopeResolutionStatus.Resolved, result.ScopeResolution.Status);
        Assert.Equal("lexical-degraded", result.Diagnostics.Mode);
    }

    [Fact]
    public async Task Importance_without_query_match_does_not_suppress_semantic_fallback()
    {
        var workspaceId = Guid.CreateVersion7();
        var workspace = new BootstrapWorkspaceCandidate(workspaceId, DepotId, null, "Project", "project");
        var unrelated = Context(workspaceId, "Unrelated information", null) with { Importance = 80 };
        var relevant = Context(workspaceId, "Database decision", null);
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([workspace]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([unrelated]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("project", It.IsAny<CancellationToken>())).ReturnsAsync(
            new WorkspaceModel(workspaceId, DepotId, "project", "Project", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var semanticRepository = new Mock<ISemanticRetrievalRepository>();
        semanticRepository.Setup(x => x.FindContextCandidatesAsync(
                It.IsAny<SemanticCandidateQuery>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SemanticContextCandidateRecord(relevant, 0.92)]);
        semanticRepository.Setup(x => x.FindDocumentCandidatesAsync(
                It.IsAny<SemanticCandidateQuery>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.Setup(x => x.GenerateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new[] { 1f, 0f, 0f })]));

        var result = await CreateService(Depot(), workspaces, repository, semanticRepository, generator)
            .BootstrapAsync(new BootstrapRequest("database", ["project"], 100), CancellationToken.None);

        Assert.Equal(relevant.Id, Assert.Single(result.Contexts).Id);
        Assert.True(result.Diagnostics.SemanticUsed);
        generator.Verify(x => x.GenerateAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Close_auto_scope_candidates_remain_ambiguous()
    {
        var first = new BootstrapWorkspaceCandidate(Guid.CreateVersion7(), DepotId, null, "Travel", "travel");
        var second = new BootstrapWorkspaceCandidate(Guid.CreateVersion7(), DepotId, null, "Travel Japan", "travel-japan");
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var depot = Depot();

        var result = await CreateService(depot, new Mock<IWorkspaceAppService>(), repository)
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
        var depot = Depot();

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            CreateService(depot, new Mock<IWorkspaceAppService>(), repository)
                .BootstrapAsync(new BootstrapRequest("anything", MaxTokens: 0), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.ContextBudgetInvalid, exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Bootstrap_queries_only_granted_workspaces()
    {
        var grantedWorkspaceId = Guid.CreateVersion7();
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var workspaceAccess = new Mock<IWorkspaceAccessContext>();
        workspaceAccess.SetupGet(x => x.HasUnrestrictedAccess).Returns(false);
        workspaceAccess.SetupGet(x => x.WorkspaceIds).Returns([grantedWorkspaceId]);
        var service = CreateService(
            Depot(),
            new Mock<IWorkspaceAppService>(),
            repository,
            workspaceAccess: workspaceAccess);

        await service.BootstrapAsync(new BootstrapRequest("database"), CancellationToken.None);

        var expectedWorkspaceIds = new HashSet<Guid> { grantedWorkspaceId };
        repository.Verify(x => x.FindScopeCandidatesAsync(
            It.Is<BootstrapQuery>(query => query.WorkspaceIds != null &&
                                          query.WorkspaceIds.SetEquals(expectedWorkspaceIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nested_workspace_scope_and_document_excerpt_use_full_path()
    {
        var parentId = Guid.CreateVersion7();
        var childId = Guid.CreateVersion7();
        var parent = new BootstrapWorkspaceCandidate(parentId, DepotId, null, "Projects", "projects");
        var child = new BootstrapWorkspaceCandidate(childId, DepotId, parentId, "Context Depot", "context-depot");
        var chunk = new BootstrapDocumentChunkCandidate(
            Guid.CreateVersion7(),
            DepotId,
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
        workspaces.Setup(x => x.ResolveAsync("projects/context-depot", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceModel(childId, DepotId, "projects/context-depot", child.Name, null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var result = await CreateService(Depot(), workspaces, repository)
            .BootstrapAsync(new BootstrapRequest("postgresql", ["projects/context-depot"], 100), CancellationToken.None);

        Assert.Equal("projects/context-depot", Assert.Single(result.ScopeResolution.Workspaces));
        Assert.Equal("projects/context-depot/docs/README.md", Assert.Single(result.Documents).Path);
    }

    [Fact]
    public async Task Semantic_scope_fallback_reuses_one_query_embedding_for_final_retrieval()
    {
        var workspaceId = Guid.CreateVersion7();
        var workspace = new BootstrapWorkspaceCandidate(workspaceId, DepotId, null, "Travel", "travel");
        var context = Context(workspaceId, "Prefer hotels.", "travel.accommodation");
        var semanticRepository = new Mock<ISemanticRetrievalRepository>();
        var queryVectors = new List<float[]>();
        var semanticCandidate = new SemanticContextCandidateRecord(context, 0.92);
        semanticRepository
            .Setup(x => x.FindContextCandidatesAsync(
                It.IsAny<SemanticCandidateQuery>(),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()))
            .Callback<SemanticCandidateQuery, ReadOnlyMemory<float>, CancellationToken>((_, vector, _) => queryVectors.Add(vector.ToArray()))
            .ReturnsAsync([semanticCandidate]);
        semanticRepository
            .Setup(x => x.FindDocumentCandidatesAsync(
                It.IsAny<SemanticCandidateQuery>(),
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var repository = new Mock<IBootstrapRepository>();
        repository.Setup(x => x.FindScopeCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([workspace]);
        repository.Setup(x => x.FindContextCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.FindDocumentCandidatesAsync(It.IsAny<BootstrapQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.Setup(x => x.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new[] { 1f, 0f, 0f })]));
        var service = CreateService(
            Depot(),
            new Mock<IWorkspaceAppService>(),
            repository,
            semanticRepository,
            generator);

        var result = await service.BootstrapAsync(new BootstrapRequest("之前喜欢的住宿方式", MaxTokens: 100), CancellationToken.None);

        Assert.Equal(ScopeResolutionStatus.Resolved, result.ScopeResolution.Status);
        Assert.Equal(context.Id, Assert.Single(result.Contexts).Id);
        Assert.True(result.Diagnostics.SemanticUsed);
        Assert.Equal(2, queryVectors.Count);
        Assert.Equal(queryVectors[0], queryVectors[1]);
        generator.Verify(x => x.GenerateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<EmbeddingGenerationOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static BootstrapContextCandidate Context(Guid workspaceId, string content, string? key) => new(
        Guid.CreateVersion7(),
        DepotId,
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

    private static Mock<ICurrentDepotContext> Depot()
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(DepotId);
        return depot;
    }

    private static ContextBootstrapAppService CreateService(
        Mock<ICurrentDepotContext> depot,
        Mock<IWorkspaceAppService> workspaces,
        Mock<IBootstrapRepository> repository,
        Mock<ISemanticRetrievalRepository>? semanticRepository = null,
        Mock<IEmbeddingGenerator<string, Embedding<float>>>? generator = null,
        Mock<IWorkspaceAccessContext>? workspaceAccess = null)
    {
        semanticRepository ??= new Mock<ISemanticRetrievalRepository>();
        var services = new Mock<IServiceProvider>();
        if (generator is not null)
        {
            services.Setup(x => x.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>)))
                .Returns(generator.Object);
        }
        var embeddingGenerator = new EmbeddingGeneratorService(
            services.Object,
            new StaticOptionsSnapshot<EmbeddingOptions>(new EmbeddingOptions { Dimensions = generator is null ? 1536 : 3 }),
            new HighConfidenceSecretDetector(),
            new EmbeddingResultValidator(),
            NullLogger<EmbeddingGeneratorService>.Instance);
        if (workspaceAccess is null)
        {
            workspaceAccess = new Mock<IWorkspaceAccessContext>();
            workspaceAccess.SetupGet(x => x.HasUnrestrictedAccess).Returns(true);
        }
        var retrievalOptions = new Mock<IOptionsMonitor<RetrievalOptions>>();
        retrievalOptions.SetupGet(x => x.CurrentValue).Returns(new RetrievalOptions());
        return new(
            depot.Object,
            workspaceAccess.Object,
            workspaces.Object,
            repository.Object,
            semanticRepository.Object,
            embeddingGenerator,
            new SemanticFallbackDecider(),
            new SemanticWorkspaceAggregator(),
            new HybridCandidateRanker(),
            new RetrievalDeduplicator(),
            retrievalOptions.Object,
            new ContextBudgetAllocator(),
            TimeProvider.System,
            NullLogger<ContextBootstrapAppService>.Instance);
    }
}
