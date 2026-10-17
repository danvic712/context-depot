using System.Text.Json;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Domain.Documents;
using Moq;

namespace ContextDepot.Application.Tests.Documents;

public sealed class DocumentLinkValidatorTests
{
    private readonly Guid depotId = Guid.NewGuid();
    private readonly Guid workspaceId = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> repository = new();
    private readonly Mock<IMarkdownStore> store = new();
    private DocumentLinkValidator Create() => new(repository.Object, store.Object);
    private Task<DocumentLinkValidation> Validate(string content) =>
        Create().ValidateAsync(depotId, workspaceId, "project", "docs/source.md", content, default);

    [Theory]
    [InlineData("C:\\docs\\guide.md")]
    [InlineData("docs/guide:old.md")]
    [InlineData("docs/line\nbreak.md")]
    public void Stored_paths_reject_platform_specific_absolute_paths_and_control_characters(string path) =>
        Assert.Throws<ContextDepot.Application.Shared.Exceptions.ContextDepotApplicationException>(() => DocumentPath.Normalize(path));

    [Theory]
    [InlineData("[Bad](../../outside.md)", "invalid_path")]
    [InlineData("[Bad](../../outside.txt)", "invalid_path")]
    [InlineData("[Bad](../..)", "invalid_path")]
    [InlineData("[Bad](%2e%2e/%2e%2e/outside.md)", "invalid_path")]
    [InlineData("[Bad](file:///tmp/guide.md)", "invalid_protocol")]
    [InlineData("[Bad](javascript:alert%281%29)", "invalid_protocol")]
    [InlineData("[Bad](javascript&#x3A;alert%281%29)", "invalid_protocol")]
    [InlineData("[Bad](//example.com/guide.md)", "invalid_protocol")]
    [InlineData("<ftp://example.com/guide.md>", "invalid_protocol")]
    [InlineData("[Bad](bad%GG.md)", "invalid_path")]
    [InlineData("[Bad](bad%FF.md)", "invalid_path")]
    [InlineData("[Bad](#%E4%B8)", "invalid_path")]
    public async Task Unsafe_links_return_actionable_diagnostics_without_loading_targets(string content, string code)
    {
        var result = await Validate("# Source\n\n" + content);
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(3, error.Line);
        Assert.Equal(1, error.Column);
        Assert.NotEmpty(error.Href);
        repository.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Code_images_unused_definitions_and_raw_HTML_are_not_linked_documents()
    {
        var result = await Validate("""
            # Source

            `[Code](javascript:example)`

            ```md
            [Fenced](../../outside.md)
            ```

                [Indented](../../outside.md)

            ![Image](../../outside.md)

            ![[Image label](../../outside.md)](image.png)

            <div>
            [Raw HTML block](../../outside.md)
            </div>

            [unused]: javascript:example

            [Web](https://example.com/guide.md)
            """);
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        repository.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reference_and_table_links_resolve_against_canonical_targets_and_reuse_lookups()
    {
        Target("reference/中文 指南.md", "# Real heading\n\n## 标题", "Old indexed text without this heading");
        var result = await Validate("""
            [Guide][ref]

            | Guide |
            | --- |
            | [Same target](/reference/中文%20指南.md#标题) |

            [ref]: ../reference/%E4%B8%AD%E6%96%87%20%E6%8C%87%E5%8D%97.md#real-heading
            """);
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        repository.Verify(x => x.GetByPathAsync(depotId, workspaceId, "reference/中文 指南.md", default), Times.Once);
        store.Verify(x => x.GetAsync(depotId, "project/reference/中文 指南.md", default), Times.Once);
    }

    [Fact]
    public async Task Missing_archived_unreadable_targets_and_missing_fragments_are_warnings()
    {
        Target("docs/active.md", "# Present");
        Target("docs/archived.md", "# Archived").MarkArchived(DateTimeOffset.UtcNow);
        Target("docs/no-file.md", null);
        Target("docs/unreadable.md", null);
        store.Setup(x => x.GetAsync(depotId, "project/docs/unreadable.md", default)).ThrowsAsync(new IOException());
        var result = await Validate("""
            [Missing](missing.md)
            [Archived](archived.md)
            [Missing file](no-file.md)
            [Unreadable](unreadable.md)
            [Missing heading](active.md#absent)
            [Other asset](guide.pdf)
            """);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "target_not_found", "target_archived", "target_content_unavailable", "target_content_unavailable",
            "anchor_not_found", "unsupported_local_link" }, result.Warnings.Select(warning => warning.Code));
        Assert.Equal("docs/active.md", result.Warnings[4].TargetPath);
        Assert.Equal("absent", result.Warnings[4].Fragment);
        store.Verify(x => x.GetAsync(depotId, "project/docs/archived.md", default), Times.Never);
    }

    [Fact]
    public async Task Self_links_use_the_incoming_body_and_report_real_missing_fragments()
    {
        var result = await Validate("# New heading\n\n[Local](#new-heading) [Self](./source.md#new-heading) [Old](#old-heading)");
        Assert.True(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("anchor_not_found", warning.Code);
        Assert.Equal("old-heading", warning.Fragment);
        repository.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Heading_fragments_match_the_reader_including_formatting_Unicode_and_collisions()
    {
        await using var stream = typeof(DocumentLinkValidatorTests).Assembly.GetManifestResourceStream("markdown-heading-links.json")!;
        using var fixtures = await JsonDocument.ParseAsync(stream);
        foreach (var fixture in fixtures.RootElement.EnumerateArray())
        {
            var content = fixture.GetProperty("content").GetString()!;
            foreach (var id in fixture.GetProperty("headings").EnumerateArray())
            {
                // Empty IDs are covered by duplicate identifiers (-1); bare # is a valid top link.
                var result = await Validate(content + "\n\n[Heading](#" + Uri.EscapeDataString(id.GetString()!) + ")");
                Assert.True(result.IsValid);
                Assert.Empty(result.Warnings);
            }
        }
    }

    [Fact]
    public async Task Validation_does_not_follow_target_links_or_access_other_workspaces()
    {
        Target("docs/target.md", "# Target\n\n[Unsafe](../../elsewhere.md)");
        var result = await Validate("[Target](target.md#target)");
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        repository.Verify(x => x.GetByPathAsync(depotId, workspaceId, "docs/target.md", default), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    private Document Target(string path, string? canonical, string? indexed = null)
    {
        var target = new Document(Guid.NewGuid(), depotId, workspaceId, path, "Target", DateTimeOffset.UtcNow);
        if (indexed is not null) target.Chunks.Add(new DocumentChunk(Guid.NewGuid(), depotId, target.Id, workspaceId,
            0, "", indexed, "old-hash", DateTimeOffset.UtcNow));
        repository.Setup(x => x.GetByPathAsync(depotId, workspaceId, path, default)).ReturnsAsync(target);
        store.Setup(x => x.GetAsync(depotId, "project/" + path, default))
            .ReturnsAsync(canonical is null ? null : new MarkdownDocument(path, canonical, "hash"));
        return target;
    }
}
