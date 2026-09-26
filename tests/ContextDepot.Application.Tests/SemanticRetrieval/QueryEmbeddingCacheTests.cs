using ContextDepot.Application.Embeddings;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.SemanticRetrieval;

public sealed class QueryEmbeddingCacheTests
{
    [Fact]
    public async Task Repeated_reads_reuse_one_generated_query_embedding()
    {
        var generator = CreateGenerator();
        var cache = new QueryEmbeddingCache(CreateEmbeddingService(generator));

        var first = await cache.GetOrCreateAsync("  database   design ", CancellationToken.None);
        var second = await cache.GetOrCreateAsync("database design", CancellationToken.None);

        Assert.Equal(first.ToArray(), second.ToArray());
        generator.Verify(x => x.GenerateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<EmbeddingGenerationOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Generator_failure_is_cached_for_the_request()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.Setup(x => x.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient"));
        var cache = new QueryEmbeddingCache(CreateEmbeddingService(generator));

        var first = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            cache.GetOrCreateAsync("database design", CancellationToken.None));
        var second = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            cache.GetOrCreateAsync("database design", CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.EmbeddingGeneratorUnavailable, first.ErrorCode);
        Assert.Equal(first.ErrorCode, second.ErrorCode);
        generator.Verify(x => x.GenerateAsync(
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<EmbeddingGenerationOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateGenerator()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.Setup(x => x.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new(new[] { 1f, 0f, 0f })]));
        return generator;
    }

    private static EmbeddingGeneratorService CreateEmbeddingService(
        Mock<IEmbeddingGenerator<string, Embedding<float>>> generator)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>)))
            .Returns(generator.Object);
        return new EmbeddingGeneratorService(
            services.Object,
            new StaticOptionsSnapshot<EmbeddingOptions>(new EmbeddingOptions { Dimensions = 3 }),
            new ContextDepot.Application.Shared.Safety.HighConfidenceSecretDetector(),
            new EmbeddingResultValidator(),
            NullLogger<EmbeddingGeneratorService>.Instance);
    }
}
