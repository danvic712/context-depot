namespace ContextDepot.Application.Tests.Evaluation;

public static class P1EvaluationEvaluator
{
    public static P1EvaluationResult Evaluate(IReadOnlyList<P1EvaluationCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        var relevantCases = cases.Count(@case => @case.Relevant);
        var baselineReturned = cases.Count(@case => @case.LexicalBaselineReturned);
        var p1Returned = cases.Count(@case => @case.P1Returned);
        var baselineRelevant = cases.Count(@case => @case.Relevant && @case.LexicalBaselineReturned);
        var p1Relevant = cases.Count(@case => @case.Relevant && @case.P1Returned);
        var semanticCases = cases.Where(@case => @case.QueryEmbeddingCalls > 0).ToArray();
        return new P1EvaluationResult(
            cases.Count,
            relevantCases,
            baselineReturned,
            p1Returned,
            baselineRelevant,
            p1Relevant,
            CalculateRatio(baselineRelevant, relevantCases),
            CalculateRatio(p1Relevant, relevantCases),
            CalculateRatio(baselineRelevant, baselineReturned),
            CalculateRatio(p1Relevant, p1Returned),
            cases.Select(@case => @case.QueryEmbeddingCalls).DefaultIfEmpty().Max(),
            semanticCases.Count(@case => @case.LexicalBaselineReturned));
    }

    private static double CalculateRatio(int numerator, int denominator) =>
        denominator == 0 ? 1 : numerator / (double)denominator;
}
