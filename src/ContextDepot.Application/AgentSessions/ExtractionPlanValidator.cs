using System.Text.RegularExpressions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Application.Shared.Localization;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.AgentSessions;

public sealed partial class ExtractionPlanValidator(ISecretDetector secrets)
{
    private static readonly EmbeddedLocaleCatalog Catalog = new(typeof(ExtractionPlanValidator).Assembly);
    private static readonly Regex PersonalDataVocabulary = Vocabulary("SessionMemoryPersonalDataPattern");
    private static readonly Regex UnsafeEvidenceVocabulary = Vocabulary("SessionMemoryUnsafeEvidencePattern");
    private static readonly Regex DecisionVocabulary = Vocabulary("SessionMemoryDecisionPattern");
    private static readonly Regex UnconfirmedDecisionVocabulary = Vocabulary("SessionMemoryUnconfirmedDecisionPattern");

    public ValidatedExtractionPlan Validate(IReadOnlyList<MemoryCandidate> candidates, string user)
    {
        var result = new List<ValidatedMemoryCandidate>();
        foreach (var candidate in candidates.Take(SessionCaptureOptions.MaxCandidates))
        {
            if (candidate is null || !Enum.TryParse<ContextKind>(candidate.Kind, true, out var kind) ||
                kind is not (ContextKind.Fact or ContextKind.Preference or ContextKind.Goal or ContextKind.Decision) ||
                !string.Equals(candidate.Kind, kind.ToString(), StringComparison.OrdinalIgnoreCase) ||
                candidate.Key is not { Length: > 0 and <= 200 } || !KeyPattern().IsMatch(candidate.Key) || !Safe(candidate.Key)) continue;
            if (candidate.Summary is null && (candidate.Confidence is < 0.9m or > 1m || candidate.Content != candidate.Evidence)) continue;
            var summary = candidate.Summary ?? candidate.Content;
            if (summary is not { Length: >= 8 and <= 2000 } || !Safe(summary)) continue;
            var quotes = candidate.Quotes ?? new[] { candidate.Evidence };
            if (quotes.Count is < 1 or > 3) continue;
            var fragments = new List<EvidenceFragment>();
            var invalid = false;
            foreach (var quote in quotes.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(quote) || quote.Length > 2000 || !Safe(quote) || !IsCompleteStatement(user, quote)) { invalid = true; break; }
                var start = user.IndexOf(quote, StringComparison.Ordinal);
                if (user.IndexOf(quote, start + 1, StringComparison.Ordinal) >= 0 ||
                    (kind == ContextKind.Decision && (!DecisionVocabulary.IsMatch(quote) || UnconfirmedDecisionVocabulary.IsMatch(quote))))
                { invalid = true; break; }
                fragments.Add(new(start, quote.Length, quote));
            }
            fragments.Sort((a, b) => a.Start.CompareTo(b.Start));
            if (invalid || fragments.Count == 0 || fragments.Sum(x => x.Length) > 2000 ||
                fragments.Zip(fragments.Skip(1)).Any(x => x.First.Start + x.First.Length > x.Second.Start)) continue;
            result.Add(new(result.Count, kind.ToString().ToLowerInvariant(), candidate.Key, summary, fragments.ToArray()));
        }
        return new(ValidatedExtractionPlan.CurrentVersion, result.ToArray());
    }

    private bool Safe(string value) => !secrets.Detect(value).IsSecret && !UnsafeEvidenceVocabulary.IsMatch(value) &&
        !PersonalData().IsMatch(value) && !PersonalDataVocabulary.IsMatch(value);

    internal static bool IsCompleteStatement(string user, string evidence)
    {
        var offset = user.IndexOf(evidence, StringComparison.Ordinal);
        if (offset < 0) return false;
        const string boundaries = "。！？.!?;；\n\r";
        var end = offset + evidence.Length;
        return (offset == 0 || boundaries.Contains(user[offset - 1])) &&
            (end == user.Length || boundaries.Contains(evidence[^1]));
    }

    private static Regex Vocabulary(string key) => new(string.Join("|", Catalog.GetValues(key)),
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    [GeneratedRegex(@"[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}|(?<!\d)(?:\+?\d[\d ()-]{7,}\d)(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PersonalData();

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
