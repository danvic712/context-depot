using ContextDepot.Application.Shared.Safety.Dtos;

namespace ContextDepot.Application.Shared.Safety.Contracts;

public interface ISecretDetector
{
    SecretDetectionResult Detect(string? value);
}
