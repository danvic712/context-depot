using System.Text.RegularExpressions;

namespace ContextDepot.Application.Tests.Shared.Exceptions;

public sealed partial class ExceptionSourceConventionTests
{
    [Fact]
    public void ThrowsAndValidatorsDoNotContainLiteralOrTranslatedMessages()
    {
        var source = FindRepositoryRoot().GetDirectories("src").Single();
        var violations = Directory.EnumerateFiles(source.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .SelectMany(path => LiteralErrorMessage().Matches(File.ReadAllText(path))
                .Select(match => $"{Path.GetRelativePath(source.FullName, path)}: {match.Value}"))
            .ToArray();
        Assert.Empty(violations);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ContextDepot.slnx")))
            directory = directory.Parent;
        return directory ?? throw new DirectoryNotFoundException(nameof(ExceptionSourceConventionTests));
    }

    [GeneratedRegex("(?:throw\\s+new\\s+[\\w.]+(?:<[^>]+>)?|ValidateOptionsResult\\.Fail|failures\\.Add|\\.WithMessage)\\s*\\(\\s*(?:[$@]*\\\"|ApplicationErrorMessages\\.Get\\s*\\()")]
    private static partial Regex LiteralErrorMessage();
}
