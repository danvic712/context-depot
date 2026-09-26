using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Retrieval;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Tests.Retrieval;

public sealed class HybridCandidateRankerTests
{
    [Fact]
    public void Exact_key_match_is_ranked_before_higher_semantic_candidate()
    {
        var workspaceId = Guid.CreateVersion7();
        var exact = Context(workspaceId, "project.database", "PostgreSQL is the database.");
        var semantic = Context(workspaceId, "project.storage", "PostgreSQL is the database.");
        var ranker = new HybridCandidateRanker();

        var ranked = ranker.RankContexts(
            [exact, semantic],
            "project.database",
            new Dictionary<Guid, string> { [workspaceId] = "projects/context-depot" },
            new Dictionary<Guid, double> { [exact.Id] = 0.10, [semantic.Id] = 0.99 });

        Assert.Equal(exact.Id, ranked[0].Context.Id);
        Assert.True(ranked[0].IsExactMatch);
        Assert.False(ranked[1].IsExactMatch);
    }

    [Fact]
    public void Exact_document_path_is_ranked_before_higher_semantic_candidate()
    {
        var workspaceId = Guid.CreateVersion7();
        var exact = Document(workspaceId, Guid.CreateVersion7(), "docs/database.md");
        var semantic = Document(workspaceId, Guid.CreateVersion7(), "docs/storage.md");
        var ranker = new HybridCandidateRanker();

        var ranked = ranker.RankDocuments(
            [exact, semantic],
            "docs/database.md",
            new Dictionary<Guid, string> { [workspaceId] = "projects/context-depot" },
            new Dictionary<Guid, double> { [exact.Id] = 0.10, [semantic.Id] = 0.99 });

        Assert.Equal(exact.Id, ranked[0].Document.Id);
        Assert.True(ranked[0].IsExactMatch);
        Assert.False(ranked[1].IsExactMatch);
    }

    [Fact]
    public void Chinese_document_content_has_a_lexical_score()
    {
        var workspaceId = Guid.CreateVersion7();
        var document = Document(workspaceId, Guid.CreateVersion7(), "docs/readme.md") with
        {
            Content = "数据库已配置。"
        };

        var ranked = new HybridCandidateRanker().RankDocuments(
            [document],
            "数据库",
            new Dictionary<Guid, string> { [workspaceId] = "projects/context-depot" });

        Assert.True(Assert.Single(ranked).LexicalScore > 0);
    }

    private static BootstrapContextCandidate Context(Guid workspaceId, string key, string content) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
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

    private static BootstrapDocumentChunkCandidate Document(Guid workspaceId, Guid documentId, string path) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        documentId,
        workspaceId,
        0,
        path,
        path.Contains("database", StringComparison.OrdinalIgnoreCase) ? "Database" : "Storage",
        path.Contains("database", StringComparison.OrdinalIgnoreCase) ? "Storage" : "Archive",
        "PostgreSQL is the database.",
        "hash",
        DateTimeOffset.UtcNow);
}
