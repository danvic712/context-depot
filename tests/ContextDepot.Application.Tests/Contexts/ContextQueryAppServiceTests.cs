using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.SemanticRetrieval.Dtos;
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

    [Fact]
    public async Task ContextOnlySearchSkipsLexicalAndSemanticDocumentsBeforeTheLimit()
    {
        var candidate = ContextCandidate(Guid.NewGuid(), "Database is configured.");
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.Is<ContextSearchQuery>(q => q.Kinds!.Contains(ContextKind.Fact)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ContextSearchCandidateRecord(candidate, "projects")]);
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new[] { 1f, 0f, 0f })]));
        var semantic = new Mock<ISemanticRetrievalRepository>();
        semantic.Setup(x => x.FindContextCandidatesAsync(It.Is<SemanticCandidateQuery>(q => !q.IncludeDocuments), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var result = await CreateService(repository, new Mock<IWorkspaceAppService>(), semantic, generator)
            .SearchAsync(new ContextSearchRequest("database", Kinds: [ContextKind.Fact], Limit: 1, IncludeDocuments: false), default);
        Assert.Equal(candidate.Id, Assert.Single(result.Contexts).ContextId);
        Assert.Empty(result.Documents);
        repository.Verify(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        semantic.Verify(x => x.FindDocumentCandidatesAsync(It.IsAny<SemanticCandidateQuery>(), It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()), Times.Never);
        semantic.VerifyAll();
    }

    [Fact]
    public async Task MixedResultsCarryGlobalOrderOnlyWhenRequested()
    {
        var workspace = Guid.NewGuid();
        var candidate = ContextCandidate(workspace, "Database information.");
        var document = new BootstrapDocumentChunkCandidate(Guid.NewGuid(), DepotId, Guid.NewGuid(), workspace, 0,
            "database.md", "database", "", "database database database", "hash", DateTimeOffset.UtcNow);
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindLexicalContextCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ContextSearchCandidateRecord(candidate, "projects")]);
        repository.Setup(x => x.FindLexicalDocumentCandidatesAsync(It.IsAny<ContextSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DocumentSearchCandidateRecord(document, "projects")]);
        var service = CreateService(repository, new Mock<IWorkspaceAppService>());
        var ordered = await service.SearchAsync(new ContextSearchRequest("database", IncludeResultOrder: true), default);
        Assert.Equal(0, Assert.Single(ordered.Documents).Ordinal);
        Assert.Equal(1, Assert.Single(ordered.Contexts).Ordinal);
        var original = await service.SearchAsync(new ContextSearchRequest("database"), default);
        Assert.Null(Assert.Single(original.Contexts).Ordinal);
        Assert.Null(Assert.Single(original.Documents).Ordinal);
        Assert.DoesNotContain("Ordinal", System.Text.Json.JsonSerializer.Serialize(original));
    }


    [Theory]
    [InlineData(false, "available")]
    [InlineData(true, "unavailable")]
    public async Task Get_returns_capture_evidence_only_after_context_and_workspace_authorization(bool corrupt, string state)
    {
        var workspaceId = Guid.NewGuid();
        var context = new ContextItem(Guid.NewGuid(), DepotId, workspaceId, ContextKind.Fact, "project.database", null,
            "用户使用 PostgreSQL 数据库。", DateTimeOffset.UtcNow);
        var repository = new Mock<IContextQueryRepository>();
        repository.Setup(x => x.FindContextByIdAsync(DepotId, context.Id, It.IsAny<CancellationToken>())).ReturnsAsync(context);
        var original = new[] { new EvidenceFragment(3, 5, "原始证据。") };
        var plaintext = JsonSerializer.Serialize(original, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var evidence = new ContextCaptureEvidence
        {
            ContextId = context.Id, DepotId = DepotId, WorkspaceId = workspaceId,
            UserEventId = Guid.NewGuid(), AssistantEventId = Guid.NewGuid(), ExtractorVersion = "capture-summary-v1",
            EncryptedFragments = "ciphertext", FragmentsHash = corrupt ? "BAD" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)))
        };
        repository.Setup(x => x.FindCaptureEvidenceAsync(DepotId, context.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evidence);
        var workspaces = new Mock<IWorkspaceAppService>();
        var protector = new Mock<ICaptureEvidenceProtector>();
        protector.Setup(x => x.Unprotect("ciphertext")).Returns(plaintext);
        var service = CreateService(repository, workspaces, evidenceProtector: protector.Object);
        Assert.Null(await service.GetAsync(context.Id, default));
        repository.Verify(x => x.FindCaptureEvidenceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        protector.VerifyNoOtherCalls();
        workspaces.Setup(x => x.GetAsync(workspaceId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new WorkspaceModel(workspaceId, DepotId, "project", "project", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var detail = await service.GetAsync(context.Id, default);
        Assert.NotNull(detail);
        Assert.Equal(context.Content, detail.Content);
        Assert.NotNull(detail.CaptureEvidence);
        Assert.Equal(state, detail.CaptureEvidence.State);
        Assert.Equal(evidence.UserEventId, detail.CaptureEvidence.SourceUserReceiptId);
        Assert.Equal(evidence.AssistantEventId, detail.CaptureEvidence.SourceAssistantReceiptId);
        if (corrupt) Assert.Empty(detail.CaptureEvidence.Fragments);
        else Assert.Equal(original, detail.CaptureEvidence.Fragments);
    }

    private static ContextQueryAppService CreateService(
        Mock<IContextQueryRepository> repository,
        Mock<IWorkspaceAppService> workspaces,
        Mock<ISemanticRetrievalRepository>? semanticRepository = null,
        Mock<IEmbeddingGenerator<string, Embedding<float>>>? generator = null,
        Mock<IWorkspaceAccessContext>? workspaceAccess = null,
        ICaptureEvidenceProtector? evidenceProtector = null)
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
            NullLogger<ContextQueryAppService>.Instance, evidenceProtector);
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
