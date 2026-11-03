using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Bootstrap;

namespace ContextDepot.Application.AgentSessions;

public static class RoutingCandidatePolicy
{
    public const string Version = "workspace-router-v1";

    public static RoutingCandidateSet Select(AuthorizedWorkspaceSnapshot scope, string user,
        IReadOnlyDictionary<Guid, double> similarities, bool semanticConfigured, bool semanticTruncated = false)
    {
        var version = Version + (semanticConfigured ? "" : "-lexical-only");
        if (scope.Workspaces.Count > 512 || semanticTruncated)
            return new([], version, new("ambiguous", ResultCode: "workspace_ambiguous", ResolverVersion: version));
        var tokens = BootstrapQueryTokenizer.Tokenize(user);
        var candidates = scope.Workspaces.Select(w => new RoutingCandidate(w,
            ContainsExact(user, w.Path), ContainsExact(user, w.Name),
            BootstrapQueryTokenizer.CountMatches(tokens, BootstrapQueryTokenizer.Tokenize($"{w.Name} {w.Path} {w.Description}")),
            similarities.GetValueOrDefault(w.Id)))
            .Where(c => c.HasSupport).OrderByDescending(c => c.ExactPath).ThenByDescending(c => c.ExactName)
            .ThenByDescending(c => c.MetadataMatches).ThenByDescending(c => c.MaxSimilarity)
            .ThenBy(c => c.Workspace.Path, StringComparer.Ordinal).ThenBy(c => c.Workspace.Id).ToArray();
        if (candidates.Length > 12)
            return new([], version, new("ambiguous", ResultCode: "workspace_ambiguous", ResolverVersion: version));
        return candidates.Length == 0
            ? new([], version, new("no_match", ResultCode: "no_match", ResolverVersion: version))
            : new(candidates, version);
    }

    public static bool ContainsExact(string user, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var han = value.Count(c => c is >= '\u3400' and <= '\u9fff');
        if (han == 1) return false;
        for (var start = 0; start <= user.Length - value.Length; start++)
        {
            if (!user.AsSpan(start, value.Length).Equals(value.AsSpan(), StringComparison.OrdinalIgnoreCase)) continue;
            if (char.IsAsciiLetterOrDigit(value[0]) && start > 0 && char.IsAsciiLetterOrDigit(user[start - 1])) continue;
            var end = start + value.Length;
            if (char.IsAsciiLetterOrDigit(value[^1]) && end < user.Length && char.IsAsciiLetterOrDigit(user[end])) continue;
            return true;
        }
        return false;
    }
}
