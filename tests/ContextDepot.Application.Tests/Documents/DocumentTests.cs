using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Tests.Documents;

public sealed class DocumentTests
{
    [Fact]
    public void MarkPending_clears_the_previous_index_error()
    {
        var document = new Document(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "readme.md", "README", DateTimeOffset.UtcNow);
        document.MarkFailed("MarkdownFileMissing", DateTimeOffset.UtcNow);

        document.MarkPending(DateTimeOffset.UtcNow);

        Assert.Equal(DocumentIndexStatus.Pending, document.IndexStatus);
        Assert.Null(document.LastIndexError);
    }
}
