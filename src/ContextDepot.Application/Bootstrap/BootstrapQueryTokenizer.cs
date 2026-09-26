using System.Text.RegularExpressions;

namespace ContextDepot.Application.Bootstrap;

internal static partial class BootstrapQueryTokenizer
{
    public static IReadOnlySet<string> Tokenize(string value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in TokenRegex().Matches(value.ToLowerInvariant()))
        {
            if (!IsHan(match.Value[0]))
            {
                tokens.Add(match.Value);
                continue;
            }

            for (var index = 0; index < match.Length; index++)
            {
                tokens.Add(match.Value.Substring(index, 1));
                if (index + 1 < match.Length)
                {
                    tokens.Add(match.Value.Substring(index, 2));
                }
            }
        }

        return tokens;
    }

    public static int CountMatches(IReadOnlySet<string> queryTokens, IReadOnlySet<string> contentTokens)
    {
        var hasHanBigram = queryTokens.Any(token => token.Length == 2 && IsHan(token[0]));
        return queryTokens.Count(token =>
            contentTokens.Contains(token) &&
            (!hasHanBigram || token.Length != 1 || !IsHan(token[0])));
    }

    private static bool IsHan(char value) => value is >= '\u3400' and <= '\u9fff';

    [GeneratedRegex("[a-z0-9]+|[\u3400-\u9fff]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
