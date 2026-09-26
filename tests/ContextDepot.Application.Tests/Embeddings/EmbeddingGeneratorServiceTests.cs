using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace ContextDepot.Application.Tests.Embeddings;

public sealed class EmbeddingGeneratorServiceTests
{
    [Fact]
    public async Task Valid_batch_is_returned_after_dimension_and_finite_validation()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>
            ([
                new(new float[] { 1, 0, 0 }),
                new(new float[] { 0, 1, 0 })
            ]));
        var service = CreateService(generator);

        var result = await service.GenerateAsync(["one", "two"], CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[0].Length);
        generator.Verify(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Secret_input_is_rejected_before_generator_is_called()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var service = CreateService(generator);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.GenerateAsync(["api_key: abcdefghijklmnop"], CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.SecretContentRejected, exception.ErrorCode);
        generator.Verify(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Generator_failure_is_mapped_to_a_stable_error_code()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(HttpStatusCode.ServiceUnavailable.ToString()));
        var service = CreateService(generator);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.GenerateAsync(["safe input"], CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.EmbeddingGeneratorUnavailable, exception.ErrorCode);
    }

    [Fact]
    public async Task Missing_generator_is_treated_as_a_degraded_dependency()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var services = new Mock<IServiceProvider>();
        var service = new EmbeddingGeneratorService(
            services.Object,
            new StaticOptionsSnapshot<EmbeddingOptions>(new EmbeddingOptions { Dimensions = 3 }),
            new ContextDepot.Application.Shared.Safety.HighConfidenceSecretDetector(),
            new EmbeddingResultValidator(),
            NullLogger<EmbeddingGeneratorService>.Instance);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.GenerateAsync(["safe input"], CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.EmbeddingGeneratorUnavailable, exception.ErrorCode);
        generator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invalid_dimension_is_mapped_to_a_stable_error_code()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new float[] { 1, 0 })]));
        var service = CreateService(generator);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.GenerateAsync(["safe input"], CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.EmbeddingDimensionMismatch, exception.ErrorCode);
    }

    [Fact]
    public async Task Invalid_count_is_mapped_to_a_stable_error_code()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>());
        var service = CreateService(generator);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.GenerateAsync(["safe input"], CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse, exception.ErrorCode);
    }

    [Fact]
    public async Task Non_finite_vector_value_is_mapped_to_a_stable_error_code()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new[] { float.NaN, 0, 0 })]));
        var service = CreateService(generator);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.GenerateAsync(["safe input"], CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse, exception.ErrorCode);
    }

    private static EmbeddingGeneratorService CreateService(Mock<IEmbeddingGenerator<string, Embedding<float>>> generator) =>
        new(
            CreateServices(generator),
            new StaticOptionsSnapshot<EmbeddingOptions>(new EmbeddingOptions { Dimensions = 3 }),
            new ContextDepot.Application.Shared.Safety.HighConfidenceSecretDetector(),
            new EmbeddingResultValidator(),
            NullLogger<EmbeddingGeneratorService>.Instance);

    private static IServiceProvider CreateServices(Mock<IEmbeddingGenerator<string, Embedding<float>>> generator)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>)))
            .Returns(generator.Object);
        return services.Object;
    }
}
