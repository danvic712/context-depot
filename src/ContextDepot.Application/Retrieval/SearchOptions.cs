namespace ContextDepot.Application.Retrieval;

public sealed class SearchOptions
{
    public int DefaultLimit { get; set; } = 10;

    public int MaxLimit { get; set; } = 50;
}
