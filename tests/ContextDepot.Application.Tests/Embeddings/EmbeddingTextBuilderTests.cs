using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Tests.Embeddings;

public sealed class EmbeddingTextBuilderTests
{
    [Fact]
    public void Context_text_is_stable_and_sorts_tags_without_runtime_metadata()
    {
        var builder = new ContextEmbeddingTextBuilder();
        var source = new ContextEmbeddingSource(
            "projects/portwise",
            ContextKind.Decision,
            "project.portwise.database",
            "Database decision",
            ["postgresql", "database", "postgresql"],
            "Portwise uses PostgreSQL.\r\n");

        var text = builder.Build(source);

        Assert.Equal(
            "workspace: projects/portwise\nkind: decision\nkey: project.portwise.database\ntitle: Database decision\ntags: database,postgresql\ncontent:\nPortwise uses PostgreSQL.",
            text);
        Assert.DoesNotContain(source.GetType().Name, text, StringComparison.Ordinal);
        Assert.DoesNotContain("importance", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Document_text_is_stable_and_omits_empty_heading()
    {
        var builder = new DocumentEmbeddingTextBuilder();
        var source = new DocumentChunkEmbeddingSource(
            "projects/portwise",
            "architecture/database.md",
            "Portwise Database Architecture",
            string.Empty,
            "Use PostgreSQL.\r\n");

        Assert.Equal(
            "workspace: projects/portwise\ndocument: architecture/database.md\ntitle: Portwise Database Architecture\ncontent:\nUse PostgreSQL.",
            builder.Build(source));
    }

    [Fact]
    public void Input_hash_is_stable_for_the_same_text()
    {
        var text = "workspace: projects/portwise\ncontent:\nPostgreSQL";

        Assert.Equal(EmbeddingInputHash.Compute(text), EmbeddingInputHash.Compute(text));
        Assert.NotEqual(EmbeddingInputHash.Compute(text), EmbeddingInputHash.Compute(text + "\nmore"));
    }
}
