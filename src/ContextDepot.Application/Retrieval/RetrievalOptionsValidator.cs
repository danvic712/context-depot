using ContextDepot.Application.Shared.Exceptions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Retrieval;

public sealed class RetrievalOptionsValidator : IValidateOptions<RetrievalOptions>
{
    public ValidateOptionsResult Validate(string? name, RetrievalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var semantic = options.Semantic;
        var search = options.Search;
        var valid = semantic is not null
            && search is not null
            && semantic.ScopeTopK > 0
            && semantic.CandidateTopKPerSource > 0
            && semantic.OversampleFactor is >= 1 and <= 10
            && IsBetweenZeroAndOne(semantic.RetrievalLexicalFallbackThreshold)
            && IsBetweenZeroAndOne(semantic.DedupSimilarityThreshold)
            && IsBetweenZeroAndOne(semantic.DedupTokenOverlapThreshold)
            && search.DefaultLimit > 0
            && search.MaxLimit >= search.DefaultLimit;

        return valid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(ApplicationErrorMessages.Get(ApplicationErrorCodes.EmbeddingConfigurationInvalid));
    }

    private static bool IsBetweenZeroAndOne(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value is >= 0 and <= 1;
}
