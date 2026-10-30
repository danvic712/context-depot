using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Contexts;

namespace ContextDepot.Application.Contexts;

/// <summary>Conservative acceptance policy for automatic Context creation.</summary>
public static class ContextCreateOnlyPolicy
{
    public const int MaxComparisonCount = 1000;

    public static ContextItem? FindExisting(ContextItem candidate, IReadOnlyList<ContextItem> existing)
    {
        var duplicate = existing.FirstOrDefault(context => context.Key == candidate.Key ||
            SimilarContent(context.Content, candidate.Content));
        if (duplicate is not null)
        {
            return duplicate;
        }

        if (existing.Count > MaxComparisonCount)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextCreateOnlyCapacity);
        }

        return null;
    }

    private static bool SimilarContent(string left, string right)
    {
        static HashSet<string> Bigrams(string content)
        {
            var normalized = new string(content.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return Enumerable.Range(0, Math.Max(0, normalized.Length - 1))
                .Select(index => normalized.Substring(index, 2)).ToHashSet(StringComparer.Ordinal);
        }

        var leftBigrams = Bigrams(left);
        var rightBigrams = Bigrams(right);
        if (leftBigrams.Count == 0 || rightBigrams.Count == 0)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        return (double)leftBigrams.Intersect(rightBigrams).Count() /
            Math.Min(leftBigrams.Count, rightBigrams.Count) >= 0.75;
    }
}
