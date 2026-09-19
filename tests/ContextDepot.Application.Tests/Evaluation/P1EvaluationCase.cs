namespace ContextDepot.Application.Tests.Evaluation;

public sealed record P1EvaluationCase(
    string Name,
    bool Relevant,
    bool LexicalBaselineReturned,
    bool P1Returned,
    int QueryEmbeddingCalls);
