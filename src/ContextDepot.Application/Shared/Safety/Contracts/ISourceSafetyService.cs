using ContextDepot.Application.Shared.Safety.Dtos;

namespace ContextDepot.Application.Shared.Safety.Contracts;

public interface ISourceSafetyService
{
    void EnsureSafe(string? content);

    ProvenanceDecision EvaluateProvenance(ProvenanceInput input);
}
