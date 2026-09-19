using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Embeddings;

public sealed class EmbeddingGeneratorRegistrationValidator(IServiceProvider services) : IValidateOptions<EmbeddingOptions>
{
    public ValidateOptionsResult Validate(string? name, EmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.GetService<IEmbeddingGenerator<string, Embedding<float>>>() is not null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(ApplicationErrorMessages.Get(ApplicationErrorCodes.EmbeddingConfigurationInvalid));
    }
}
