namespace ContextDepot.Application.Tests.Evaluation;

public sealed class P1FinalEvaluationTests
{
    [Fact]
    public void P1_fuzzy_recall_improves_without_precision_regression()
    {
        var result = P1EvaluationEvaluator.Evaluate(P1EvaluationDataset.Cases);

        Assert.Equal(8, result.CaseCount);
        Assert.Equal(5, result.RelevantCaseCount);
        Assert.True(result.P1Recall > result.BaselineRecall);
        Assert.True(result.P1Precision >= result.BaselinePrecision);
        Assert.Equal(1, result.MaxQueryEmbeddingCalls);
        Assert.Equal(0, result.UnnecessarySemanticCalls);
    }

    [Fact]
    public void P1_evaluation_dataset_has_clear_and_fuzzy_cases()
    {
        var cases = P1EvaluationDataset.Cases;

        Assert.Contains(cases, @case => @case.LexicalBaselineReturned && @case.QueryEmbeddingCalls == 0);
        Assert.Contains(cases, @case => !@case.LexicalBaselineReturned && @case.P1Returned && @case.QueryEmbeddingCalls == 1);
        Assert.Contains(cases, @case => !@case.Relevant && !@case.P1Returned);
    }
}
