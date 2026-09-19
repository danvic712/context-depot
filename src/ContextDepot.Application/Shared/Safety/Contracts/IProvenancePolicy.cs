using ContextDepot.Application.Shared.Safety.Dtos;

namespace ContextDepot.Application.Shared.Safety.Contracts;

public interface IProvenancePolicy
{
    ProvenanceDecision Evaluate(ProvenanceInput input);
}
