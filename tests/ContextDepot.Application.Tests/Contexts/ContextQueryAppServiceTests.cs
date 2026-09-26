using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.Contexts;

public sealed class ContextQueryAppServiceTests
{
    private static readonly Guid DepotId = Guid.Parse("0199c000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Search_is_depot_wide_by_default_without_auto_scope_resolution()
    {
        var workspaceId = Guid.CreateVersion7();
        var candidate = ContextCandidate(workspaceId, "PostgreSQL is the database.");
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ContextSearchCandidateRecord(candidate, "projects/context-depot")]);
        repository.Setup(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var workspaces = new Mock<IWorkspaceAppService>();
        var service = CreateService(repository, workspaces);

        var result = await service.SearchAsync(new ContextSearchRequest("database"), CancellationToken.None);

        var match = Assert.Single(result.Contexts);
        Assert.Equal(candidate.Id, match.ContextId);
        Assert.Equal("projects/context-depot", match.Workspace);
        workspaces.Verify(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_resolves_explicit_scope_and_descendants()
    {
        var parentId = Guid.CreateVersion7();
        var childId = Guid.CreateVersion7();
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.LoadTopologyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new WorkspaceTopology([
                new WorkspaceTreeNode(parentId, null, "projects"),
                new WorkspaceTreeNode(childId, parentId, "context-depot")
            ]));
        var service = CreateService(repository, workspaces);

        await service.SearchAsync(
            new ContextSearchRequest("database", ["projects"], IncludeDescendants: true),
            CancellationToken.None);

        repository.Verify(x => x.FindLexicalContextCandidatesAsync(
            It.Is<ContextSearchQuery>(query => query.WorkspaceIds != null &&
                                               query.WorkspaceIds.Contains(parentId) &&
                                               query.WorkspaceIds.Contains(childId)),
            It.IsAny<CancellationToken>()), Times.Once);
        workspaces.Verify(x => x.LoadTopologyAsync(It.IsAny<CancellationToken>()), Times.Once);
        workspaces.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_without_explicit_scope_uses_granted_workspaces()
    {
        var grantedWorkspaceId = Guid.CreateVersion7();
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var workspaceAccess = new Mock<IWorkspaceAccessContext>();
        workspaceAccess.SetupGet(x => x.HasUnrestrictedAccess).Returns(false);
        workspaceAccess.SetupGet(x => x.WorkspaceIds).Returns([grantedWorkspaceId]);
        var service = CreateService(
            repository,
            new Mock<IWorkspaceAppService>(),
            workspaceAccess: workspaceAccess);

        await service.SearchAsync(new ContextSearchRequest("database"), CancellationToken.None);

        var expectedWorkspaceIds = new HashSet<Guid> { grantedWorkspaceId };
        repository.Verify(x => x.FindLexicalContextCandidatesAsync(
            It.Is<ContextSearchQuery>(query => query.WorkspaceIds != null &&
                                               query.WorkspaceIds.SetEquals(expectedWorkspaceIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_reads_archived_context_in_current_depot_scope()
    {
        var workspaceId = Guid.CreateVersion7();
        var context = new ContextItem(
            Guid.CreateVersion7(),
            DepotId,
            workspaceId,
            ContextKind.Fact,
            "project.database",
            "Database",
            "PostgreSQL",
            DateTimeOffset.UtcNow);
        context.MarkArchived(DateTimeOffset.UtcNow);
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindContextByIdAsync(DepotId, context.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.GetAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceModel(workspaceId, DepotId, "projects/context-depot", "Context Depot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var service = CreateService(repository, workspaces);

        var result = await service.GetAsync(context.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ContextStatus.Archived, result.Status);
        Assert.Equal("projects/context-depot", result.Workspace);
    }

    [Fact]
    public async Task Get_does_not_return_another_depot_context()
    {
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindContextByIdAsync(DepotId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextItem?)null);
        var service = CreateService(repository, new Mock<IWorkspaceAppService>());

        var result = await service.GetAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Search_returns_lexical_degraded_result_when_embedding_is_unavailable()
    {
        var workspaceId = Guid.CreateVersion7();
        var candidate = ContextCandidate(workspaceId, "Database is configured.");
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ContextSearchCandidateRecord(candidate, "projects/context-depot")]);
        repository.Setup(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateService(repository, new Mock<IWorkspaceAppService>());

        var result = await service.SearchAsync(new ContextSearchRequest("database"), CancellationToken.None);

        Assert.Equal("lexical-degraded", result.Retrieval.Mode);
        Assert.True(result.Retrieval.RetrievalDegraded);
        Assert.Single(result.Contexts);
    }

    [Fact]
    public async Task Chinese_query_finds_canonical_context_when_embedding_is_unavailable()
    {
        var workspaceId = Guid.CreateVersion7();
        var candidate = ContextCandidate(workspaceId, "数据库已配置。");
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ContextSearchCandidateRecord(candidate, "projects/context-depot")]);
        repository.Setup(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService(repository, new Mock<IWorkspaceAppService>())
            .SearchAsync(new ContextSearchRequest("数据库"), CancellationToken.None);

        Assert.Equal(candidate.Id, Assert.Single(result.Contexts).ContextId);
        Assert.Equal("lexical-degraded", result.Retrieval.Mode);
    }

    [Fact]
    public async Task Search_rejects_empty_query_and_limit_above_configuration()
    {
        var service = CreateService(new Mock<IContextQueryRepository>(), new Mock<IWorkspaceAppService>());

        await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.SearchAsync(new ContextSearchRequest(" "), CancellationToken.None));
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.SearchAsync(new ContextSearchRequest("database", Limit: 51), CancellationToken.None));
    }

    private static ContextQueryAppService CreateService(
        Mock<IContextQueryRepository> repository,
        Mock<IWorkspaceAppService> workspaces,
        Mock<ISemanticRetrievalRepository>? semanticRepository = null,
        Mock<IEmbeddingGenerator<string, Embedding<float>>>? generator = null,
        Mock<IWorkspaceAccessContext>? workspaceAccess = null)
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(DepotId);
        if (workspaceAccess is null)
        {
            workspaceAccess = new Mock<IWorkspaceAccessContext>();
            workspaceAccess.SetupGet(x => x.HasUnrestrictedAccess).Returns(true);
            workspaceAccess.Setup(x => x.CanAccess(It.IsAny<Guid>())).Returns(true);
        }
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
        var options = new Mock<IOptionsMonitor<RetrievalOptions>>();
        options.SetupGet(x => x.CurrentValue).Returns(new RetrievalOptions());
        return new ContextQueryAppService(
            depot.Object,
            workspaceAccess.Object,
            repository.Object,
            workspaces.Object,
            (semanticRepository ?? new Mock<ISemanticRetrievalRepository>()).Object,
            embeddingGenerator,
            new SemanticFallbackDecider(),
            new HybridCandidateRanker(),
            new RetrievalDeduplicator(),
            options.Object,
            TimeProvider.System,
            NullLogger<ContextQueryAppService>.Instance);
    }

    private static BootstrapContextCandidate ContextCandidate(Guid workspaceId, string content) => new(
        Guid.CreateVersion7(),
        DepotId,
        workspaceId,
        ContextKind.Fact,
        null,
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
}
