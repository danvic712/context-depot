using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Embeddings;

public sealed class EmbeddingOptionsValidator : IValidateOptions<EmbeddingOptions>
{
    public ValidateOptionsResult Validate(string? name, EmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            failures.Add(ApplicationErrorMessages.Get(ApplicationErrorCodes.EmbeddingConfigurationInvalid));
        }

        if (string.IsNullOrWhiteSpace(options.Model) || options.Dimensions <= 0)
        {
            failures.Add(ApplicationErrorMessages.Get(ApplicationErrorCodes.EmbeddingConfigurationInvalid));
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
