namespace ContextDepot.Application.AgentSessions;

public sealed class SessionCaptureOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 3;
    public int RetentionHours { get; set; } = 24;
    public int MaxCallsPerHour { get; set; } = 30;
    public int MaxPendingEventsPerKey { get; set; } = 200;
    public int ExtractionTimeoutSeconds { get; set; } = 60;
    public int RoutingMaxAttempts { get; set; } = 3;
    public int RoutingTimeoutSeconds { get; set; } = 15;
    public int RoutingLeaseSeconds { get; set; } = 45;
    public int RoutingCandidateLimit { get; set; } = 12;
    public int RoutingMetadataScanLimit { get; set; } = 512;
    public int RoutingSemanticTopK { get; set; } = 128;
    public double RoutingMinSemanticSimilarity { get; set; } = 0.82;
    public int RoutingMaxOutputTokens { get; set; } = 512;
    public int MaxRoutingResponseChars { get; set; } = 8192;
    public int MaxEvidenceFragmentsPerCandidate { get; set; } = 3;
    public int MaxEvidenceCharsPerCandidate { get; set; } = 2000;
    public int ExtractionPlanMaxChars { get; set; } = 32768;
    public int PersistenceMaxAttempts { get; set; } = 3;
    public const int MaxContentLength = 20_000;
    public const int MaxCandidates = 3;
}
