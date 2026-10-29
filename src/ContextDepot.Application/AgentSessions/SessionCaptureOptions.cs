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
    public const int MaxContentLength = 20_000;
    public const int MaxCandidates = 3;
}
