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

public sealed partial class MemoryCaptureProcessor(ISessionCaptureAuthorization authorization,
    IMemoryExtractionService extraction, IContextAppService contexts, ISecretDetector secrets)
{
    private static readonly EmbeddedLocaleCatalog Catalog = new(typeof(MemoryCaptureProcessor).Assembly);
    private static readonly Regex PersonalDataVocabulary = Vocabulary("SessionMemoryPersonalDataPattern");
    private static readonly Regex UnsafeEvidenceVocabulary = Vocabulary("SessionMemoryUnsafeEvidencePattern");
    private static readonly Regex DecisionVocabulary = Vocabulary("SessionMemoryDecisionPattern");
    private static readonly Regex UnconfirmedDecisionVocabulary = Vocabulary("SessionMemoryUnconfirmedDecisionPattern");

    public async Task<string> ProcessAsync(CapturedTurn turn, CancellationToken cancellationToken)
    {
        var workspace = await authorization.AuthorizeAsync(turn, cancellationToken);
        if (workspace is null) return "skipped_unauthorized";
        // Never send obvious credentials to the configured provider.
        if (secrets.Detect(turn.UserContent).IsSecret || secrets.Detect(turn.AssistantContent).IsSecret)
            return "skipped_sensitive";
        var candidates = await extraction.ExtractAsync(turn.UserContent, turn.AssistantContent, cancellationToken);
        var saved = false;
        foreach (var candidate in candidates.Take(SessionCaptureOptions.MaxCandidates))
        {
            if (!TryValidate(candidate, turn.UserContent, out var kind)) continue;
            // Recheck after the model call as well; the persistence transaction also locks the live grant.
            if (await authorization.AuthorizeAsync(turn, cancellationToken) is null) return "skipped_unauthorized";
            var result = await contexts.SaveAsync(new SaveContextCommand(workspace, kind, candidate.Content,
                Key: candidate.Key, Confidence: candidate.Confidence, SourceType: SourceType.Agent,
                SourceAgent: turn.Agent, SourceRef: $"session-capture:{turn.UserEventId}:{turn.AssistantEventId}",
                WriteMode: ContextWriteMode.CreateOnly), cancellationToken);
            saved |= result.Outcome == ContextDepot.Application.Contexts.Enums.SaveContextOutcome.Created;
        }
        return saved ? "saved" : "skipped_no_new_memory";
    }

    private bool TryValidate(MemoryCandidate candidate, string user, out ContextKind kind)
    {
        kind = default;
        // P0 stores the exact user quote. A model paraphrase is not server-verifiable evidence.
        return candidate.Kind is not null && Enum.TryParse(candidate.Kind, true, out kind) &&
            kind is ContextKind.Fact or ContextKind.Preference or ContextKind.Goal or ContextKind.Decision &&
            string.Equals(candidate.Kind, kind.ToString(), StringComparison.OrdinalIgnoreCase) &&
            candidate.Confidence is >= 0.9m and <= 1m && candidate.Key is { Length: > 0 and <= 200 } &&
            KeyPattern().IsMatch(candidate.Key) && candidate.Content is { Length: >= 8 and <= 2000 } &&
            candidate.Evidence == candidate.Content && IsCompleteStatement(user, candidate.Evidence) &&
            !secrets.Detect(candidate.Content).IsSecret && !secrets.Detect(candidate.Key).IsSecret &&
            !UnsafeEvidenceVocabulary.IsMatch(candidate.Content) && !PersonalData().IsMatch(candidate.Content) && !PersonalDataVocabulary.IsMatch(candidate.Content) &&
            (kind != ContextKind.Decision || (DecisionVocabulary.IsMatch(candidate.Evidence) &&
                !UnconfirmedDecisionVocabulary.IsMatch(candidate.Evidence)));
    }

    private static bool IsCompleteStatement(string user, string evidence)
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
