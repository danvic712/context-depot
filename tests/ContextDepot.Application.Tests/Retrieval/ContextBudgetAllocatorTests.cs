using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Retrieval;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Tests.Retrieval;

public sealed class ContextBudgetAllocatorTests
{
    [Fact]
    public void Contexts_and_documents_share_one_budget()
    {
        var context = new ContextModel(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ContextKind.Fact,
            null,
            "Database",
            "PostgreSQL",
            [],
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
            DateTimeOffset.UtcNow);
        var document = new DocumentExcerptModel(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "docs/database.md",
            "Storage",
            "PostgreSQL",
            "hash");
        var allocator = new ContextBudgetAllocator();

        var result = allocator.Allocate([context], [document], 2);

        Assert.True(result.EstimatedTokens <= 2);
        Assert.True(result.Contexts.Count + result.Documents.Count <= 2);
    }
}
