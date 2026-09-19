using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Shared.Safety.Dtos;

public sealed record SecretDetectionResult(bool IsSecret, string ErrorCode = ApplicationErrorCodes.SecretContentRejected)
{
    public static SecretDetectionResult Safe { get; } = new(false, string.Empty);

    public static SecretDetectionResult Rejected { get; } = new(true);
}
