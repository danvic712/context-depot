using ContextDepot.Application.Retrieval;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Tests.Retrieval;

public sealed class RetrievalOptionsTests
{
    [Fact]
    public void Invalid_retrieval_thresholds_fail_validation()
    {
        var options = new RetrievalOptions();
        options.Semantic.RetrievalLexicalFallbackThreshold = 1.1;

        var result = new RetrievalOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
    }
}
