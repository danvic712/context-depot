using System.Text;
using ContextDepot.Application.Embeddings.Dtos;

namespace ContextDepot.Application.Embeddings;

public sealed class ContextEmbeddingTextBuilder
{
    public string Build(ContextEmbeddingSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.WorkspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Content);

        var builder = new StringBuilder()
            .Append("workspace: ").AppendLine(Normalize(source.WorkspacePath))
            .Append("kind: ").AppendLine(source.Kind.ToString().ToLowerInvariant());

        AppendOptional(builder, "key", source.Key);
        AppendOptional(builder, "title", source.Title);

        var tags = (source.Tags ?? [])
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (tags.Length > 0)
        {
            builder.Append("tags: ").AppendLine(string.Join(',', tags));
        }

        builder.Append("content:").AppendLine();
        builder.Append(Normalize(source.Content));
        return builder.ToString();
    }

    private static void AppendOptional(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.Append(label).Append(": ").AppendLine(Normalize(value));
        }
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
}
