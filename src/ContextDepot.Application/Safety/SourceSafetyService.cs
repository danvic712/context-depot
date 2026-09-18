using ContextDepot.Application.Abstractions;

namespace ContextDepot.Application.Safety;

public interface ISourceSafetyService
{
    void EnsureSafe(string? content);

    ProvenanceDecision EvaluateProvenance(ProvenanceInput input);
}

public sealed class SourceSafetyService(ISecretDetector secretDetector, IProvenancePolicy provenancePolicy) : ISourceSafetyService
{
    public void EnsureSafe(string? content)
    {
        if (secretDetector.Detect(content).IsSecret)
        {
            throw new ContextDepotApplicationException("SecretContentRejected", "Source content was rejected by the safety policy.");
        }
    }

    public ProvenanceDecision EvaluateProvenance(ProvenanceInput input) => provenancePolicy.Evaluate(input);
}
