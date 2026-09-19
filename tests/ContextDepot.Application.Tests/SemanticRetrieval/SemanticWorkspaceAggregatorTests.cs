using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Tests.SemanticRetrieval;

public sealed class SemanticWorkspaceAggregatorTests
{
    private static readonly Guid OwnerId = Guid.CreateVersion7();

    [Fact]
    public void Strong_single_workspace_signal_is_resolved()
    {
        var workspaceId = Guid.CreateVersion7();
        var result = new SemanticWorkspaceAggregator().Aggregate(
            [new SemanticContextCandidateRecord(Context(workspaceId), 0.92)],
            [],
            new Dictionary<Guid, string> { [workspaceId] = "projects/portwise" });

        Assert.Equal(ScopeResolutionStatus.Resolved, result.Resolution.Status);
        Assert.Equal("projects/portwise", Assert.Single(result.Resolution.Workspaces));
        Assert.Contains(workspaceId, result.WorkspaceIds!);
    }

    [Fact]
    public void Close_workspace_signals_remain_ambiguous()
    {
        var firstId = Guid.CreateVersion7();
        var secondId = Guid.CreateVersion7();
        var result = new SemanticWorkspaceAggregator().Aggregate(
            [
                new SemanticContextCandidateRecord(Context(firstId), 0.9),
                new SemanticContextCandidateRecord(Context(secondId), 0.88)
            ],
            [],
            new Dictionary<Guid, string>
            {
                [firstId] = "personal/travel/japan",
                [secondId] = "personal/travel/yunnan"
            });

        Assert.Equal(ScopeResolutionStatus.Ambiguous, result.Resolution.Status);
        Assert.Equal(2, result.Resolution.Workspaces.Count);
        Assert.Empty(result.WorkspaceIds!);
    }

    [Fact]
    public void Weak_signal_falls_back_to_owner_wide_scope()
    {
        var workspaceId = Guid.CreateVersion7();
        var result = new SemanticWorkspaceAggregator().Aggregate(
            [new SemanticContextCandidateRecord(Context(workspaceId), 0.42)],
            [],
            new Dictionary<Guid, string> { [workspaceId] = "projects/portwise" });

        Assert.Equal(ScopeResolutionStatus.Broad, result.Resolution.Status);
        Assert.Null(result.WorkspaceIds);
    }

    private static BootstrapContextCandidate Context(Guid workspaceId) => new(
        Guid.CreateVersion7(),
        OwnerId,
        workspaceId,
        ContextKind.Decision,
        "project.database",
        "Database decision",
        "Use PostgreSQL.",
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
