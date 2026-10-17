using System.Text;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ContextDepot.Application.Documents;

/// <summary>
/// Reads rendered links and heading text from the syntax tree, excluding code blocks and images.
/// </summary>
internal sealed record MarkdownDocumentAnalysis(IReadOnlyList<MarkdownDocumentLink> Links, HashSet<string> HeadingIds)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder { PreciseSourceLocation = true }
        .UsePipeTables().UseEmphasisExtras(EmphasisExtraOptions.Strikethrough).Build();

    public static MarkdownDocumentAnalysis Parse(string content)
    {
        var tree = Markdown.Parse(content, Pipeline);
        var links = tree.Descendants<LinkInline>().Where(link => !link.IsImage && !InsideImage(link))
            .Select(link => new MarkdownDocumentLink(link.Url ?? string.Empty, link.Line + 1, link.Column + 1))
            .Concat(tree.Descendants<AutolinkInline>().Where(link => !InsideImage(link)).Select(link =>
                new MarkdownDocumentLink(link.IsEmail ? "mailto:" + link.Url : link.Url, link.Line + 1, link.Column + 1)))
            .OrderBy(link => link.Line).ThenBy(link => link.Column).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var heading in tree.Descendants<HeadingBlock>())
        {
            var text = new StringBuilder();
            AppendText(heading.Inline, text);
            var original = MarkdownHeadingSlugger.Slug(text.ToString());
            var id = original;
            for (var suffix = 1; !ids.Add(id); suffix++) id = original + "-" + suffix;
        }
        return new(links, ids);
    }

    private static bool InsideImage(Inline inline)
    {
        for (var parent = inline.Parent; parent is not null; parent = parent.Parent)
            if (parent is LinkInline { IsImage: true }) return true;
        return false;
    }

    private static void AppendText(Inline? inline, StringBuilder text)
    {
        switch (inline)
        {
            case LinkInline { IsImage: true }: break;
            case LiteralInline literal: text.Append(literal.Content.ToString()); break;
            case CodeInline code: text.Append(code.Content); break;
            case HtmlEntityInline entity: text.Append(entity.Transcoded.ToString()); break;
            case LineBreakInline: text.Append('\n'); break;
            case AutolinkInline link: text.Append(link.Url); break;
            case ContainerInline container:
                foreach (var child in container) AppendText(child, text);
                break;
        }
    }
}

internal sealed record MarkdownDocumentLink(string Href, int Line, int Column);
