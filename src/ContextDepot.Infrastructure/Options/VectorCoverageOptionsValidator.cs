using Microsoft.Extensions.Options;
using ContextDepot.Infrastructure.Exceptions;

namespace ContextDepot.Infrastructure.Options;

public sealed class VectorCoverageOptionsValidator : IValidateOptions<VectorCoverageOptions>
{
    public ValidateOptionsResult Validate(string? name, VectorCoverageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.CacheDurationSeconds is >= 1 and <= 300
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(InfrastructureErrorCodes.VectorCoverageSettingsInvalid);
    }
}
