using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class RoutingCandidatePolicyTests
{
    [Theory]
    [InlineData(.8199, false)]
    [InlineData(.82, true)]
    public void Semantic_support_uses_routing_threshold(double similarity, bool supported)
    {
        var workspace = new RoutingWorkspace(Guid.NewGuid(), "unrelated", "unrelated", null);
        var scope = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(), [workspace]);
        var result = RoutingCandidatePolicy.Select(scope, "database", new Dictionary<Guid, double> { [workspace.Id] = similarity }, true);
        Assert.Equal(supported, result.Candidates.Count == 1);
    }

    [Fact]
    public void Sentinel_blocks_even_a_strong_exact_match()
    {
        var workspace = new RoutingWorkspace(Guid.NewGuid(), "photos", "photos", null);
        var scope = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(), [workspace]);
        Assert.Equal("ambiguous", RoutingCandidatePolicy.Select(scope, "photos", new Dictionary<Guid, double>(), true, true).Outcome?.Decision);
    }

    [Theory]
    [InlineData("distributed", false)]
    [InlineData("distributed database", true)]
    public void Description_support_needs_two_tokens(string user, bool supported)
    {
        var workspace = new RoutingWorkspace(Guid.NewGuid(), "work", "work", "distributed database");
        var scope = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(), [workspace]);
        Assert.Equal(supported, RoutingCandidatePolicy.Select(scope, user, new Dictionary<Guid, double>(), false).Candidates.Count == 1);
    }

    [Theory]
    [InlineData("database", "data", false)]
    [InlineData("use data now", "data", true)]
    [InlineData("摄影项目", "摄影", true)]
    [InlineData("摄影项目", "影", false)]
    public void Exact_support_requires_boundaries(string user, string name, bool expected) =>
        Assert.Equal(expected, RoutingCandidatePolicy.ContainsExact(user, name));

    [Fact]
    public void Single_authorized_workspace_does_not_imply_support()
    {
        var scope = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(), [new(Guid.NewGuid(), "photos", "photos", null)]);
        Assert.Equal("no_match", RoutingCandidatePolicy.Select(scope, "continue", new Dictionary<Guid, double>(), false).Outcome?.Decision);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(513)]
    public void Truncation_is_ambiguous(int count)
    {
        var scope = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(),
            Enumerable.Range(0, count).Select(i => new RoutingWorkspace(Guid.NewGuid(), "photos", $"photos/{i}", null)).ToArray());
        Assert.Equal("ambiguous", RoutingCandidatePolicy.Select(scope, "photos", new Dictionary<Guid, double>(), true).Outcome?.Decision);
    }
}
