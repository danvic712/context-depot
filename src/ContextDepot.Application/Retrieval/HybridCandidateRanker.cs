using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Retrieval;

public sealed class HybridCandidateRanker
{
    private const double SemanticWeight = 0.55;
    private const double LexicalWeight = 0.20;
    private const double QualityWeight = 0.15;
    private const double VerificationWeight = 0.10;
    private const double ExactKeyScore = 1.00;
    private const double ExactPathScore = 0.95;
    private const double ExactTitleScore = 0.90;
    private const double LexicalNormalizationFactor = 10;

    private readonly ScopeCandidateRanker lexicalRanker = new();

    public IReadOnlyList<RankedContextCandidate> RankContexts(
        IReadOnlyList<BootstrapContextCandidate> candidates,
        string query,
        IReadOnlyDictionary<Guid, string> workspacePaths,
        IReadOnlyDictionary<Guid, double>? semanticScores = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(workspacePaths);

        var tokens = BootstrapQueryTokenizer.Tokenize(query);
        return candidates
            .Select(candidate =>
            {
                var workspacePath = workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty);
                var rawLexicalScore = lexicalRanker.ScoreContext(candidate, workspacePath, query, tokens);
                var exactScore = GetContextExactScore(candidate, workspacePath, query);
                var semanticScore = ClampScore(semanticScores?.GetValueOrDefault(candidate.Id) ?? 0);
                var qualityScore = GetContextQualityScore(candidate);
                var verificationScore = GetContextVerificationScore(candidate);
                var lexicalScore = NormalizeLexicalScore(rawLexicalScore);
                var score = CalculateScore(exactScore, lexicalScore, semanticScore, qualityScore, verificationScore);
                return new RankedContextCandidate(
                    candidate,
                    exactScore,
                    lexicalScore,
                    semanticScore,
                    qualityScore,
                    verificationScore,
                    score);
            })
            .OrderByDescending(candidate => candidate.IsExactMatch)
            .ThenByDescending(candidate => candidate.ExactScore)
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.SemanticScore)
            .ThenByDescending(candidate => candidate.Context.Importance)
            .ThenByDescending(candidate => candidate.Context.UpdatedAt)
            .ThenBy(candidate => candidate.SourceIdentity)
            .ToArray();
    }

    public IReadOnlyList<RankedDocumentCandidate> RankDocuments(
        IReadOnlyList<BootstrapDocumentChunkCandidate> candidates,
        string query,
        IReadOnlyDictionary<Guid, string> workspacePaths,
        IReadOnlyDictionary<Guid, double>? semanticScores = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(workspacePaths);

        var tokens = BootstrapQueryTokenizer.Tokenize(query);
        return candidates
            .Select(candidate =>
            {
                var workspacePath = workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty);
                var rawLexicalScore = lexicalRanker.ScoreDocument(candidate, workspacePath, query, tokens);
                var exactScore = GetDocumentExactScore(candidate, workspacePath, query);
                var semanticScore = ClampScore(semanticScores?.GetValueOrDefault(candidate.Id) ?? 0);
                const double qualityScore = 0.50;
                const double verificationScore = 0.50;
                var lexicalScore = NormalizeLexicalScore(rawLexicalScore);
                var score = CalculateScore(exactScore, lexicalScore, semanticScore, qualityScore, verificationScore);
                return new RankedDocumentCandidate(
                    candidate,
                    exactScore,
                    lexicalScore,
                    semanticScore,
                    qualityScore,
                    verificationScore,
                    score);
            })
            .OrderByDescending(candidate => candidate.IsExactMatch)
            .ThenByDescending(candidate => candidate.ExactScore)
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.SemanticScore)
            .ThenBy(candidate => candidate.Document.Ordinal)
            .ThenBy(candidate => candidate.SourceIdentity)
            .ToArray();
    }

    private static double GetContextExactScore(
        BootstrapContextCandidate candidate,
        string workspacePath,
        string query)
    {
        var exactScore = 0d;
        if (ContainsQuery(query, candidate.Key))
        {
            exactScore = Math.Max(exactScore, ExactKeyScore);
        }

        if (ContainsQuery(query, workspacePath))
        {
            exactScore = Math.Max(exactScore, ExactPathScore);
        }

        if (ContainsQuery(query, candidate.Title))
        {
            exactScore = Math.Max(exactScore, ExactTitleScore);
        }

        return exactScore;
    }

    private static double GetDocumentExactScore(
        BootstrapDocumentChunkCandidate candidate,
        string workspacePath,
        string query)
    {
        var exactScore = 0d;
        if (ContainsQuery(query, candidate.Path) ||
            ContainsQuery(query, CombinePath(workspacePath, candidate.Path)))
        {
            exactScore = Math.Max(exactScore, ExactPathScore);
        }

        if (ContainsQuery(query, candidate.Title) ||
            ContainsQuery(query, candidate.HeadingPath))
        {
            exactScore = Math.Max(exactScore, ExactTitleScore);
        }

        return exactScore;
    }

    private static double GetContextQualityScore(BootstrapContextCandidate candidate)
    {
        var importance = Math.Clamp(candidate.Importance / 100d, 0, 1);
        var confidence = Math.Clamp((double)(candidate.Confidence ?? 0.5m), 0, 1);
        var provenance = candidate.ProvenanceTrust switch
        {
            ProvenanceTrust.Attested => 1d,
            ProvenanceTrust.Asserted => 0.7d,
            _ => 0.25d
        };
        return (importance + confidence + provenance) / 3;
    }

    private static double GetContextVerificationScore(BootstrapContextCandidate candidate)
    {
        var verification = candidate.VerificationStatus switch
        {
            VerificationStatus.Verified => 1d,
            VerificationStatus.Explicit => 0.8d,
            VerificationStatus.Inferred => 0.55d,
            _ => 0.25d
        };
        var provenance = candidate.ProvenanceTrust switch
        {
            ProvenanceTrust.Attested => 1d,
            ProvenanceTrust.Asserted => 0.7d,
            _ => 0.25d
        };
        return (verification * 0.7) + (provenance * 0.3);
    }

    private static double CalculateScore(
        double exactScore,
        double lexicalScore,
        double semanticScore,
        double qualityScore,
        double verificationScore) =>
        (semanticScore * SemanticWeight) +
        (lexicalScore * LexicalWeight) +
        (qualityScore * QualityWeight) +
        (verificationScore * VerificationWeight) +
        exactScore;

    private static double NormalizeLexicalScore(double score) =>
        Math.Clamp(score / LexicalNormalizationFactor, 0, 1);

    private static double ClampScore(double score) =>
        double.IsNaN(score) || double.IsInfinity(score) ? 0 : Math.Clamp(score, 0, 1);

    private static bool ContainsQuery(string query, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        query.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string CombinePath(string workspacePath, string path) =>
        string.IsNullOrWhiteSpace(workspacePath) ? path : workspacePath.TrimEnd('/') + "/" + path.TrimStart('/');
}
