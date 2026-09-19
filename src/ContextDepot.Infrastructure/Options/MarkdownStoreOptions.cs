namespace ContextDepot.Infrastructure.Options;

public sealed class MarkdownStoreOptions
{
    public string MarkdownRoot { get; set; } = "data/context-depot/knowledge";

    public string Root => MarkdownRoot;
}
