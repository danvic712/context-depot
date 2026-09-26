using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Workspaces;

namespace ContextDepot.Application.Bootstrap;

internal sealed class WorkspaceScopeResolver(ScopeCandidateRanker ranker)
{
    public IReadOnlyDictionary<Guid, string> BuildPaths(IReadOnlyList<BootstrapWorkspaceCandidate> workspaces)
    {
        return new WorkspaceTopology(workspaces.Select(workspace =>
            new WorkspaceTreeNode(workspace.Id, workspace.ParentWorkspaceId, workspace.Slug))).Paths;
    }

    public (ScopeResolution Resolution, HashSet<Guid>? ScopeIds) Resolve(
        IReadOnlySet<string> queryTokens,
        IReadOnlyList<BootstrapWorkspaceCandidate> workspaces,
        IReadOnlyDictionary<Guid, string> workspacePaths,
        IReadOnlyList<BootstrapContextCandidate> contexts,
        IReadOnlyList<BootstrapDocumentChunkCandidate> chunks)
    {
        if (queryTokens.Count == 0)
        {
            return (new ScopeResolution(ScopeResolutionStatus.Broad, []), null);
        }

        var scores = new List<(BootstrapWorkspaceCandidate Workspace, double Score)>();
        foreach (var workspace in workspaces)
        {
            var path = workspacePaths.GetValueOrDefault(workspace.Id, workspace.Slug);
            var pathTokens = BootstrapQueryTokenizer.Tokenize($"{path} {workspace.Name}");
            var pathScore = BootstrapQueryTokenizer.CountMatches(queryTokens, pathTokens);
            var contextScore = contexts
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => ranker.ScoreContext(x, path, string.Empty, queryTokens) - x.Importance / 100d)
                .DefaultIfEmpty(0)
                .Max();
            var documentScore = chunks
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => ranker.ScoreDocument(x, path, string.Empty, queryTokens))
                .DefaultIfEmpty(0)
                .Max();
            var score = pathScore * 5 + contextScore + documentScore;
            if (score > 0)
            {
                scores.Add((workspace, score));
            }
        }

        if (scores.Count == 0)
        {
            return (new ScopeResolution(ScopeResolutionStatus.Broad, []), []);
        }

        var ordered = scores.OrderByDescending(x => x.Score).ToArray();
        var best = ordered[0];
        var close = ordered.Skip(1).Where(x => x.Score >= best.Score * 0.85).Select(x => x.Workspace).ToArray();
        if (close.Length > 0)
        {
            var candidates = new[] { best.Workspace }
                .Concat(close)
                .Select(x => workspacePaths.GetValueOrDefault(x.Id, x.Slug))
                .ToArray();
            return (new ScopeResolution(ScopeResolutionStatus.Ambiguous, candidates), []);
        }

        return (
            new ScopeResolution(ScopeResolutionStatus.Resolved, [workspacePaths.GetValueOrDefault(best.Workspace.Id, best.Workspace.Slug)]),
            [best.Workspace.Id]);
    }

}
