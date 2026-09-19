using System.Text.RegularExpressions;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;

namespace ContextDepot.Application.Shared.Safety;

public sealed partial class HighConfidenceSecretDetector : ISecretDetector
{
    public SecretDetectionResult Detect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SecretDetectionResult.Safe;
        }

        return SecretRegex().IsMatch(value) ? SecretDetectionResult.Rejected : SecretDetectionResult.Safe;
    }

    [GeneratedRegex(
        "(?i)(?:-----BEGIN (?:RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----|\\b(?:sk-[a-zA-Z0-9]{20,}|gh[pousr]_[a-zA-Z0-9]{20,}|github_pat_[a-zA-Z0-9_]{20,}|AKIA[0-9A-Z]{16})\\b|\\b(?:aws_secret_access_key|client_secret|access_token|refresh_token|private_key|api_key)\\s*[:=]\\s*[\\\"']?[A-Za-z0-9_\\-/.+=]{16,})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex SecretRegex();
}
