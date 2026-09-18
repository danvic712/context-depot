using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Documents;

namespace ContextDepot.Application.Tests;

public sealed class DocumentChunkingTests
{
    [Fact]
    public void Heading_chunking_is_deterministic_and_preserves_heading_context()
    {
        const string markdown = "# Root\nintro\n\n## Child\nbody";
        var chunker = new HeadingAwareMarkdownChunker();

        var first = chunker.Chunk(markdown);
        var second = chunker.Chunk(markdown);

        Assert.Equal(first, second);
        Assert.Equal(2, first.Count);
        Assert.Equal("Root", first[0].HeadingPath);
        Assert.Equal("Root > Child", first[1].HeadingPath);
        Assert.Contains("body", first[1].Content);
    }

    [Theory]
    [InlineData("../secret.md")]
    [InlineData("/absolute.md")]
    [InlineData("notes.txt")]
    [InlineData("section/../secret.md")]
    public void Unsafe_document_paths_are_rejected(string path)
    {
        var exception = Assert.Throws<ContextDepotApplicationException>(() => DocumentPath.Normalize(path));

        Assert.Equal("InvalidDocumentPath", exception.ErrorCode);
    }
}
