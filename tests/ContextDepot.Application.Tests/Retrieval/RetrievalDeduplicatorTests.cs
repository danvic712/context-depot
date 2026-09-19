using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Tests.Retrieval;

public sealed class RetrievalDeduplicatorTests
{
    [Fact]
    public void Unkeyed_same_workspace_and_kind_duplicate_is_suppressed_without_mutating_source()
    {
        var workspaceId = Guid.CreateVersion7();
        var first = Context(workspaceId, ContextKind.Preference, null, "Prefers hotels over homestays.");
        var duplicate = Context(workspaceId, ContextKind.Preference, null, "Prefers hotels over homestays.");
        var ranked = new[]
        {
            Ranked(first, 2),
            Ranked(duplicate, 1)
        };
        var deduplicator = new RetrievalDeduplicator(Options.Create(new RetrievalOptions()));

        var result = deduplicator.DeduplicateContexts(ranked);

        var retained = Assert.Single(result);
        Assert.Equal(first.Id, retained.Context.Id);
        Assert.Equal("Prefers hotels over homestays.", duplicate.Content);
    }

    [Fact]
    public void Event_is_never_semantically_suppressed()
    {
        var workspaceId = Guid.CreateVersion7();
        var first = Context(workspaceId, ContextKind.Event, null, "Conference starts tomorrow.");
        var duplicate = Context(workspaceId, ContextKind.Event, null, "Conference starts tomorrow.");
        var deduplicator = new RetrievalDeduplicator(Options.Create(new RetrievalOptions()));

        var result = deduplicator.DeduplicateContexts([Ranked(first, 2), Ranked(duplicate, 1)]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Different_stable_keys_are_never_semantically_suppressed()
    {
        var workspaceId = Guid.CreateVersion7();
        var first = Context(workspaceId, ContextKind.Preference, "travel.hotel", "Prefers hotels.");
        var second = Context(workspaceId, ContextKind.Preference, "travel.lodging", "Prefers hotels.");
        var deduplicator = new RetrievalDeduplicator(Options.Create(new RetrievalOptions()));

        var result = deduplicator.DeduplicateContexts([Ranked(first, 2), Ranked(second, 1)]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Document_duplicate_is_suppressed_only_inside_same_document()
    {
        var workspaceId = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();
        var first = Document(workspaceId, documentId, 0);
        var duplicate = Document(workspaceId, documentId, 1);
        var otherDocument = Document(workspaceId, Guid.CreateVersion7(), 0);
        var deduplicator = new RetrievalDeduplicator(Options.Create(new RetrievalOptions()));

        var result = deduplicator.DeduplicateDocuments([
            Ranked(first, 3),
            Ranked(duplicate, 2),
            Ranked(otherDocument, 1)
        ]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, candidate => candidate.Document.DocumentId == otherDocument.DocumentId);
    }

    private static RankedContextCandidate Ranked(BootstrapContextCandidate context, double score) =>
        new(context, 0, 0, 1, 0.5, 0.5, score);

    private static RankedDocumentCandidate Ranked(BootstrapDocumentChunkCandidate document, double score) =>
        new(document, 0, 0, 1, 0.5, 0.5, score);

    private static BootstrapContextCandidate Context(
        Guid workspaceId,
        ContextKind kind,
        string? key,
        string content) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        workspaceId,
        kind,
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

    private static BootstrapDocumentChunkCandidate Document(Guid workspaceId, Guid documentId, int ordinal) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        documentId,
        workspaceId,
        ordinal,
        "docs/travel.md",
        "Travel",
        "Hotels",
        "Prefers hotels over homestays.",
        "hash-" + ordinal,
        DateTimeOffset.UtcNow);
}
