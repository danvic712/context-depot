using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Safety.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Embeddings;

public sealed class EmbeddingGeneratorService(
    IServiceProvider services,
    IOptionsSnapshot<EmbeddingOptions> options,
    ISecretDetector secretDetector,
    EmbeddingResultValidator resultValidator,
    ILogger<EmbeddingGeneratorService> logger)
{
    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
        {
            return [];
        }

        for (var index = 0; index < inputs.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(inputs[index]);
            if (secretDetector.Detect(inputs[index]).IsSecret)
            {
                logger.LogWarning(
                    "{ErrorCode} blocked embedding generation for input index {InputIndex}.",
                    ApplicationErrorCodes.SecretContentRejected,
                    index);
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SecretContentRejected);
            }
        }

        var profile = EmbeddingProfile.From(options.Value);
        var generator = services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (generator is null)
        {
            logger.LogWarning(
                "{ErrorCode} is unavailable because no embedding generator is registered.",
                ApplicationErrorCodes.EmbeddingGeneratorUnavailable);
            throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingGeneratorUnavailable);
        }

        GeneratedEmbeddings<Embedding<float>> generated;
        try
        {
            generated = await generator.GenerateAsync(
                inputs,
                new EmbeddingGenerationOptions
                {
                    ModelId = profile.Model,
                    Dimensions = profile.Dimensions
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "{ErrorCode} prevented embedding generation for {InputCount} inputs.",
                ApplicationErrorCodes.EmbeddingGeneratorUnavailable,
                inputs.Count);
            throw new ContextDepotApplicationException(ApplicationErrorCodes.EmbeddingGeneratorUnavailable);
        }

        return resultValidator.Validate(generated, inputs.Count, profile.Dimensions);
    }
}
