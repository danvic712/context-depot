using Microsoft.Extensions.Options;

namespace ContextDepot.Application.IndexRepair;

public sealed class IndexRepairOptionsValidator : IValidateOptions<IndexRepairOptions>
{
    public ValidateOptionsResult Validate(string? name, IndexRepairOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.PollIntervalSeconds > 0 &&
               options.BatchSize is >= 1 and <= 256 &&
               options.MaxBatchesPerCycle is >= 1 and <= 100
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Index repair settings are outside their supported ranges.");
    }
}
