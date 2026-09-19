using System.Text.RegularExpressions;
using ContextDepot.Application.Documents.Dtos;

namespace ContextDepot.Application.Documents;

public sealed partial class HeadingAwareMarkdownChunker
{
    public IReadOnlyList<MarkdownChunk> Chunk(string content)
    {
        var chunks = new List<MarkdownChunk>();
        var headingStack = new List<(int Level, string Title)>();
        var currentLines = new List<string>();
        var currentHeading = string.Empty;

        void Flush()
        {
            var value = string.Join('\n', currentLines).Trim();
            if (value.Length == 0)
            {
                currentLines.Clear();
                return;
            }

            chunks.Add(new MarkdownChunk(currentHeading, value, DocumentContentHasher.Compute(value)));
            currentLines.Clear();
        }

        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var match = HeadingRegex().Match(line);
            if (match.Success)
            {
                Flush();
                var level = match.Groups[1].Value.Length;
                var title = match.Groups[2].Value.Trim();
                while (headingStack.Count > 0 && headingStack[^1].Level >= level)
                {
                    headingStack.RemoveAt(headingStack.Count - 1);
                }

                headingStack.Add((level, title));
                currentHeading = string.Join(" > ", headingStack.Select(x => x.Title));
            }

            currentLines.Add(line);
        }

        Flush();
        return chunks;
    }

    [GeneratedRegex("^(#{1,6})\\s+(.+?)\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();
}
