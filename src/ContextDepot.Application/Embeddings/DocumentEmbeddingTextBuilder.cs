using System.Text;
using ContextDepot.Application.Embeddings.Dtos;

namespace ContextDepot.Application.Embeddings;

public sealed class DocumentEmbeddingTextBuilder
{
    public string Build(DocumentChunkEmbeddingSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.WorkspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.DocumentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Content);

        var builder = new StringBuilder()
            .Append("workspace: ").AppendLine(Normalize(source.WorkspacePath))
            .Append("document: ").AppendLine(Normalize(source.DocumentPath))
            .Append("title: ").AppendLine(Normalize(source.Title));

        if (!string.IsNullOrWhiteSpace(source.HeadingPath))
        {
            builder.Append("heading: ").AppendLine(Normalize(source.HeadingPath));
        }

        builder.Append("content:").AppendLine();
        builder.Append(Normalize(source.Content));
        return builder.ToString();
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
}
