using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Infrastructure.AgentSessions;
using ContextDepot.Infrastructure.Repositories;

namespace ContextDepot.Infrastructure.Tests.AgentSessions;

public sealed class WorkspaceRouterTests
{
    [Theory]
    [InlineData("wait", "budget_deferred", "budget_deferred")]
    [InlineData("expired", "transient_failure", "expired")]
    [InlineData("lease_lost", "transient_failure", "lease_lost")]
    public void Budget_outcomes_preserve_wait_deadline(string status, string decision, string code)
    {
        var deadline = DateTimeOffset.UtcNow.AddHours(1);
        var result = SessionRoutingCandidateRepository.ReservationDecision(new(status, deadline));
        Assert.Equal(decision, result.Decision);
        Assert.Equal(code, result.ResultCode);
        Assert.Equal(status == "wait" ? deadline : (DateTimeOffset?)null, result.NotBefore);
    }

    [Fact]
    public void Selection_requires_current_authorization_and_actual_support()
    {
        var workspace = new RoutingWorkspace(Guid.NewGuid(), "photos", "photos", null);
        var authorized = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(), [workspace]);
        var json = "{\"decision\":\"resolved\",\"candidateId\":\"c1\",\"evidence\":\"photos\"}";
        var supported = new RoutingCandidateSet([new(workspace, true, false, 0, 0)]);
        Assert.Equal("permanent_failure", AgentFrameworkWorkspaceRouter.ParseResponse(json, "photos", supported,
            authorized with { Workspaces = [] }).Decision);
        var unsupported = new RoutingCandidateSet([new(workspace, false, false, 1, .81)]);
        Assert.Equal("permanent_failure", AgentFrameworkWorkspaceRouter.ParseResponse(json, "photos", unsupported, authorized).Decision);
        Assert.Equal("permanent_failure", AgentFrameworkWorkspaceRouter.ParseResponse(new string(' ', 8193), "photos", supported, authorized).Decision);
    }

    [Theory]
    [InlineData("{\"decision\":\"resolved\",\"candidateId\":\"c1\",\"evidence\":\"photos\"}", "resolved")]
    [InlineData("{\"decision\":\"no_match\"}", "no_match")]
    [InlineData("{\"decision\":\"ambiguous\"}", "ambiguous")]
    [InlineData("{\"decision\":\"no_match\",\"evidence\":\"photos\"}", "permanent_failure")]
    [InlineData("{\"decision\":\"no_match\",\"decision\":\"no_match\"}", "permanent_failure")]
    [InlineData("{\"decision\":\"resolved\",\"candidateId\":\"c2\",\"evidence\":\"photos\"}", "permanent_failure")]
    [InlineData("{\"decision\":\"resolved\",\"candidateId\":\"c1\",\"evidence\":\"invented\"}", "permanent_failure")]
    [InlineData("{\"decision\":\"no_match\",\"confidence\":1}", "permanent_failure")]
    [InlineData("[]", "permanent_failure")]
    public void Strict_response_contract(string json, string expected)
    {
        var workspace = new RoutingWorkspace(Guid.NewGuid(), "photos", "photos", null);
        var scope = new AuthorizedWorkspaceSnapshot(Guid.NewGuid(), Guid.NewGuid(), [workspace]);
        var set = new RoutingCandidateSet([new(workspace, false, true, 0, 0)]);
        Assert.Equal(expected, AgentFrameworkWorkspaceRouter.ParseResponse(json, "I use photos", set, scope).Decision);
    }
}
