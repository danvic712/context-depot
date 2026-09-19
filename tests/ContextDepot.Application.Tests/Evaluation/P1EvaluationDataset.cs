namespace ContextDepot.Application.Tests.Evaluation;

public static class P1EvaluationDataset
{
    public static IReadOnlyList<P1EvaluationCase> Cases { get; } =
    [
        new("clear stable key query", true, true, true, 0),
        new("clear document path query", true, true, true, 0),
        new("fuzzy preference query", true, false, true, 1),
        new("fuzzy project decision query", true, false, true, 1),
        new("fuzzy document knowledge query", true, false, true, 1),
        new("low confidence semantic candidate", false, false, false, 1),
        new("superseded current truth", false, false, false, 0),
        new("unrelated owner-wide item", false, false, false, 0)
    ];
}
