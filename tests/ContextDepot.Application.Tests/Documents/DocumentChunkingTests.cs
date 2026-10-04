using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Tests.Documents;

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
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Line_endings_preserve_chunk_contents_headings_and_hashes(string newline)
    {
        var markdown = string.Join(newline, "# Root", "intro", "", "## Child", "body", "", "");
        var chunks = new HeadingAwareMarkdownChunker().Chunk(markdown);

        Assert.Equal(new[]
        {
            new MarkdownChunk("Root", "# Root\nintro", DocumentContentHasher.Compute("# Root\nintro")),
            new MarkdownChunk("Root > Child", "## Child\nbody", DocumentContentHasher.Compute("## Child\nbody"))
        }, chunks);
    }

    [Fact]
    public void Mixed_line_endings_and_unicode_separators_preserve_document_content()
    {
        var chunks = new HeadingAwareMarkdownChunker().Chunk("# Root\r\nleft\u2028right\rmiddle\nlast");

        Assert.Equal("# Root\nleft\u2028right\nmiddle\nlast", Assert.Single(chunks).Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\r\n ")]
    public void Empty_or_whitespace_documents_have_no_chunks(string markdown)
    {
        Assert.Empty(new HeadingAwareMarkdownChunker().Chunk(markdown));
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
