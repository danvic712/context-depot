using System.Text.RegularExpressions;

namespace ContextDepot.Application.Bootstrap;

internal static partial class BootstrapQueryTokenizer
{
    public static IReadOnlySet<string> Tokenize(string value) =>
        TokenRegex().Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex("[a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
