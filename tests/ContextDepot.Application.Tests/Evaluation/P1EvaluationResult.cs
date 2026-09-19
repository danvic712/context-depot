namespace ContextDepot.Application.Tests.Evaluation;

public sealed record P1EvaluationResult(
    int CaseCount,
    int RelevantCaseCount,
    int BaselineReturnedCount,
    int P1ReturnedCount,
    int BaselineRelevantCount,
    int P1RelevantCount,
    double BaselineRecall,
    double P1Recall,
    double BaselinePrecision,
    double P1Precision,
    int MaxQueryEmbeddingCalls,
    int UnnecessarySemanticCalls);
