using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;

namespace ContextDepot.Application.Bootstrap;

internal sealed class ScopeCandidateRanker
{
    public double ScoreContext(
        BootstrapContextCandidate context,
        string workspacePath,
        string query,
        IReadOnlySet<string> tokens)
    {
        var contentTokens = BootstrapQueryTokenizer.Tokenize(
            $"{workspacePath} {context.Title} {context.Content} {context.TagsJson} {context.MetadataJson}");
        var score = BootstrapQueryTokenizer.CountMatches(tokens, contentTokens);
        if (!string.IsNullOrWhiteSpace(context.Key) && query.Contains(context.Key, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(context.Title) && query.Contains(context.Title, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        return score + context.Importance / 100d;
    }

    public double ScoreDocument(
        BootstrapDocumentChunkCandidate chunk,
        string workspacePath,
        string query,
        IReadOnlySet<string> tokens)
    {
        var contentTokens = BootstrapQueryTokenizer.Tokenize(
            $"{workspacePath} {chunk.Path} {chunk.Title} {chunk.HeadingPath} {chunk.Content}");
        var score = BootstrapQueryTokenizer.CountMatches(tokens, contentTokens);
        if (!string.IsNullOrWhiteSpace(chunk.Path) && query.Contains(chunk.Path, StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }

        if (!string.IsNullOrWhiteSpace(chunk.Title) && query.Contains(chunk.Title, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (!string.IsNullOrWhiteSpace(chunk.HeadingPath) && query.Contains(chunk.HeadingPath, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        return score;
    }
}
