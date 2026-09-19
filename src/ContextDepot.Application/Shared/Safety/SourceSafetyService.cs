using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;

namespace ContextDepot.Application.Shared.Safety;

public sealed class SourceSafetyService(ISecretDetector secretDetector, IProvenancePolicy provenancePolicy) : ISourceSafetyService
{
    public void EnsureSafe(string? content)
    {
        if (secretDetector.Detect(content).IsSecret)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SecretContentRejected);
        }
    }

    public ProvenanceDecision EvaluateProvenance(ProvenanceInput input) => provenancePolicy.Evaluate(input);
}
