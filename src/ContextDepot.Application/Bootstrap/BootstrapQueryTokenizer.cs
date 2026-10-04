using System.Text.RegularExpressions;

namespace ContextDepot.Application.Bootstrap;

public static partial class BootstrapQueryTokenizer
{
    public static IReadOnlySet<string> Tokenize(string value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var normalized = value.ToLowerInvariant();
        foreach (var match in TokenRegex().EnumerateMatches(normalized))
        {
            var text = normalized.AsSpan(match.Index, match.Length);
            if (!IsHan(text[0]))
            {
                tokens.Add(text.ToString());
                continue;
            }

            for (var index = 0; index < match.Length; index++)
            {
                tokens.Add(text.Slice(index, 1).ToString());
                if (index + 1 < match.Length)
                {
                    tokens.Add(text.Slice(index, 2).ToString());
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
