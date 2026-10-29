using ContextDepot.Application.Contexts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Contexts;

namespace ContextDepot.Application.Tests.Contexts;

public sealed class ContextCreateOnlyPolicyTests
{
    private static ContextItem Context(string key, string content, ContextKind kind = ContextKind.Decision) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), kind, key, null, content, DateTimeOffset.UtcNow);

    [Fact]
    public void Existing_key_wins_even_when_new_content_and_kind_differ()
    {
        var existing = Context("project.database", "SQLite", ContextKind.Fact);
        var candidate = Context("project.database", "PostgreSQL");
        Assert.Same(existing, ContextCreateOnlyPolicy.FindExisting(candidate, [existing]));
        Assert.Equal("SQLite", existing.Content);
        Assert.Equal(ContextStatus.Active, existing.Status);
    }

    [Theory]
    [InlineData("采用 PostgreSQL 作为项目数据库。", "采用 PostgreSQL 作为项目数据库！")]
    [InlineData("Use PostgreSQL as the database.", "USE POSTGRESQL AS THE DATABASE")]
    public void Formatting_changes_and_alternate_keys_do_not_bypass_duplicate_checks(string existingContent, string newContent)
    {
        var existing = Context("project.database", existingContent);
        Assert.Same(existing, ContextCreateOnlyPolicy.FindExisting(Context("database.alias", newContent), [existing]));
    }

    [Fact]
    public void Independent_knowledge_can_be_created()
    {
        var existing = Context("project.database", "PostgreSQL stores project data.");
        Assert.Null(ContextCreateOnlyPolicy.FindExisting(Context("editor.theme", "I prefer a light editor theme."), [existing]));
    }

    [Fact]
    public void Exceeding_comparison_budget_cannot_skip_duplicate_safety()
    {
        var existing = Enumerable.Range(0, ContextCreateOnlyPolicy.MaxComparisonCount + 1)
            .Select(index => Context($"fact.{index}", "PostgreSQL stores project data.")).ToArray();
        var error = Assert.Throws<ContextDepotApplicationException>(() =>
            ContextCreateOnlyPolicy.FindExisting(Context("editor.theme", "I prefer a light editor theme."), existing));
        Assert.Equal(ApplicationErrorCodes.ContextCreateOnlyCapacity, error.ErrorCode);
    }
}
