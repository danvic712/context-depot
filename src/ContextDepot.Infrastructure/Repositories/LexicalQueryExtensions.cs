using ContextDepot.Application.Bootstrap;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ContextDepot.Infrastructure.Repositories;

internal static class LexicalQueryExtensions
{
    public static IOrderedQueryable<ContextItem> OrderForQuery(
        this IQueryable<ContextItem> source, string query, IReadOnlyDictionary<Guid, string> workspacePaths)
    {
        var (patterns, workspaceIds) = Prepare(query, workspacePaths);
        if (patterns.Length == 0)
        {
            return source.OrderByDescending(context => context.Importance).ThenByDescending(context => context.UpdatedAt);
        }

        var normalizedQuery = query.ToLowerInvariant();
        return source
            .Where(context => workspaceIds.Contains(context.WorkspaceId) ||
                patterns.Any(pattern => Regex.IsMatch(
                    (context.Content + " " + (context.Title ?? "") + " " + (context.Key ?? "") +
                    " " + context.TagsJson + " " + context.MetadataJson).ToLower(), pattern)))
            .OrderByDescending(context => context.Key != null && normalizedQuery.Contains(context.Key.ToLower()))
            .ThenByDescending(context => patterns.Count(pattern => Regex.IsMatch(
                (context.Content + " " + (context.Title ?? "") + " " + (context.Key ?? "") +
                " " + context.TagsJson + " " + context.MetadataJson).ToLower(), pattern)))
            .ThenByDescending(context => context.Importance)
            .ThenByDescending(context => context.UpdatedAt)
            .ThenBy(context => context.Id);
    }

    public static IOrderedQueryable<DocumentChunk> OrderForQuery(
        this IQueryable<DocumentChunk> source, string query, IReadOnlyDictionary<Guid, string> workspacePaths)
    {
        var (patterns, workspaceIds) = Prepare(query, workspacePaths);
        if (patterns.Length == 0)
        {
            return source.OrderByDescending(chunk => chunk.UpdatedAt).ThenBy(chunk => chunk.DocumentId).ThenBy(chunk => chunk.Ordinal);
        }

        return source
            .Where(chunk => workspaceIds.Contains(chunk.WorkspaceId) ||
                patterns.Any(pattern => Regex.IsMatch(
                    (chunk.Content + " " + chunk.Document!.Path + " " + chunk.Document.Title + " " + chunk.HeadingPath).ToLower(), pattern)))
            .OrderByDescending(chunk => patterns.Count(pattern => Regex.IsMatch(
                (chunk.Content + " " + chunk.Document!.Path + " " + chunk.Document.Title + " " + chunk.HeadingPath).ToLower(), pattern)))
            .ThenByDescending(chunk => chunk.UpdatedAt)
            .ThenBy(chunk => chunk.DocumentId)
            .ThenBy(chunk => chunk.Ordinal);
    }

    private static (string[] Patterns, Guid[] WorkspaceIds) Prepare(
        string query, IReadOnlyDictionary<Guid, string> workspacePaths)
    {
        var tokens = BootstrapQueryTokenizer.Tokenize(query);
        var hasHanBigram = tokens.Any(token => token.Length == 2 && token[0] is >= '\u3400' and <= '\u9fff');
        // Tokens contain no regex metacharacters. Match complete ASCII tokens and
        // Han bigrams so substring false positives cannot exhaust candidate limits.
        var patterns = tokens.Where(token => !hasHanBigram || token.Length != 1 || token[0] is not (>= '\u3400' and <= '\u9fff'))
            .Select(token => token[0] is >= '\u3400' and <= '\u9fff'
                ? token : "(^|[^a-z0-9])" + token + "([^a-z0-9]|$)").ToArray();
        var workspaceIds = workspacePaths.Where(path =>
                BootstrapQueryTokenizer.CountMatches(tokens, BootstrapQueryTokenizer.Tokenize(path.Value)) > 0)
            .Select(path => path.Key).ToArray();
        return (patterns, workspaceIds);
    }
}
