using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.SemanticRetrieval.Dtos;

namespace ContextDepot.Application.SemanticRetrieval;

public sealed class SemanticWorkspaceAggregator
{
    private const int CandidateCapPerWorkspace = 3;
    private const double StrongScore = 0.65;
    private const double CloseRatio = 0.9;

    public SemanticScopeResolution Aggregate(
        IReadOnlyList<SemanticContextCandidateRecord> contexts,
        IReadOnlyList<SemanticDocumentCandidateRecord> documents,
        IReadOnlyDictionary<Guid, string> workspacePaths)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(workspacePaths);

        var workspaceScores = contexts
            .Select(candidate => (candidate.WorkspaceId, candidate.Similarity))
            .Concat(documents.Select(candidate => (candidate.WorkspaceId, candidate.Similarity)))
            .Where(candidate => candidate.Similarity > 0 && workspacePaths.ContainsKey(candidate.WorkspaceId))
            .GroupBy(candidate => candidate.WorkspaceId)
            .Select(group =>
            {
                var scores = group
                    .OrderByDescending(candidate => candidate.Similarity)
                    .Take(CandidateCapPerWorkspace)
                    .Select(candidate => candidate.Similarity)
                    .ToArray();
                var score = scores.Length == 0
                    ? 0
                    : scores[0] + scores.Skip(1).Sum() * 0.3;
                return (WorkspaceId: group.Key, Score: Math.Clamp(score, 0, 1));
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => workspacePaths[candidate.WorkspaceId], StringComparer.Ordinal)
            .ToArray();

        if (workspaceScores.Length == 0 || workspaceScores[0].Score < StrongScore)
        {
            return new SemanticScopeResolution(
                new ScopeResolution(ScopeResolutionStatus.Broad, []),
                null);
        }

        var best = workspaceScores[0];
        var close = workspaceScores
            .Skip(1)
            .Where(candidate => candidate.Score >= best.Score * CloseRatio)
            .ToArray();
        if (close.Length > 0)
        {
            var candidates = new[] { best }
                .Concat(close)
                .Select(candidate => workspacePaths[candidate.WorkspaceId])
                .ToArray();
            return new SemanticScopeResolution(
                new ScopeResolution(ScopeResolutionStatus.Ambiguous, candidates),
                new HashSet<Guid>());
        }

        return new SemanticScopeResolution(
            new ScopeResolution(ScopeResolutionStatus.Resolved, [workspacePaths[best.WorkspaceId]]),
            new HashSet<Guid> { best.WorkspaceId });
    }
}
