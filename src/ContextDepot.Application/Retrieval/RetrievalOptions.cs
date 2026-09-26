namespace ContextDepot.Application.Retrieval;

public sealed class RetrievalOptions
{
    public SemanticRetrievalOptions Semantic { get; set; } = new();

    public SearchOptions Search { get; set; } = new();
}
