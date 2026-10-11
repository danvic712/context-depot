using ContextDepot.Application.AgentContext.Dtos;

namespace ContextDepot.Application.AgentContext.Contracts;

public interface IAgentContextAppService
{
    Task<AgentContextResult> PrepareAsync(PrepareAgentContextRequest request, CancellationToken cancellationToken);
    AgentContextResult AssessActivity(AgentActivityRequest request);
}
