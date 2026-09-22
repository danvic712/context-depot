using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Options;

public sealed class VectorCoverageOptionsValidator : IValidateOptions<VectorCoverageOptions>
{
    public ValidateOptionsResult Validate(string? name, VectorCoverageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.CacheDurationSeconds is >= 1 and <= 300
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Vector coverage cache duration must be between 1 and 300 seconds.");
    }
}
