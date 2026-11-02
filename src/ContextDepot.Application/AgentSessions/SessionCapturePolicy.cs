using System.Linq.Expressions;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.AgentSessions;

/// <summary>Capture lifecycle decisions shared by atomic repository operations.</summary>
public static class SessionCapturePolicy
{
    public static void Purge(SessionCaptureEvent message, DateTimeOffset now)
    {
        message.EncryptedContent = null;
        message.ContentPurgedAt ??= now;
        message.EncryptedExtractionPlan = null;
        message.ExtractionPlanHash = null;
    }

    public static void ApplyLateArrival(SessionCaptureEvent message, bool hasTerminalPartner, DateTimeOffset now)
    {
        if (!hasTerminalPartner) return;
        Complete(message, "skipped_incomplete", SessionCaptureDisposition.Completed, 0, now);
        Purge(message, now);
    }

    public static Expression<Func<SessionCaptureEvent, bool>> RequiresCleanup(SessionCaptureOptions options, DateTimeOffset now)
    {
        var cutoff = now.AddHours(-options.RetentionHours);
        return e => (e.CaptureExpiresAt != null && e.CaptureExpiresAt <= now &&
                (e.EncryptedContent != null || e.EncryptedExtractionPlan != null || e.Status == "pending" || e.Status == "processing")) ||
            (e.CaptureExpiresAt == null && ((e.CreatedAt < cutoff && (e.Status == "pending" ||
                (e.Status == "processing" && e.LeaseUntil <= now))) ||
                ((e.Status == "completed" || e.Status == "failed") && e.ProcessedAt < cutoff && e.EncryptedContent != null))) ||
            (e.Status == "processing" && e.LeaseUntil <= now &&
                ((e.EncryptedExtractionPlan == null && e.AttemptCount >= options.MaxAttempts) ||
                 (e.EncryptedExtractionPlan != null && e.PersistenceAttemptCount >= options.PersistenceMaxAttempts)));
    }

    public static void Cleanup(SessionCaptureEvent message, bool hasPartner, SessionCaptureOptions options, DateTimeOffset now)
    {
        var expired = message.CaptureExpiresAt is { } deadline ? deadline <= now : message.CreatedAt < now.AddHours(-options.RetentionHours);
        if (expired && (message.Status == "pending" || message.Status == "processing") &&
            (message.CaptureExpiresAt is not null || message.Status != "processing" || message.LeaseUntil <= now))
        {
            Complete(message, hasPartner ? "skipped_expired" : "skipped_incomplete", SessionCaptureDisposition.Completed, options.MaxAttempts, now);
            Purge(message, now);
        }
        else if (message.Status == "processing" && message.LeaseUntil <= now &&
            (message.EncryptedExtractionPlan == null ? message.AttemptCount >= options.MaxAttempts : message.PersistenceAttemptCount >= options.PersistenceMaxAttempts))
            Complete(message, "retry_exhausted", SessionCaptureDisposition.Failed, options.MaxAttempts, now);
        if ((message.Status == "completed" || message.Status == "failed") &&
            (message.CaptureExpiresAt is { } expires ? expires <= now : message.ProcessedAt < now.AddHours(-options.RetentionHours)))
            Purge(message, now);
    }

    public static Expression<Func<SessionCaptureEvent, bool>> WithinRetention(SessionCaptureOptions options, DateTimeOffset now)
    {
        var cutoff = now.AddHours(-options.RetentionHours);
        return e => e.CaptureExpiresAt != null ? e.CaptureExpiresAt > now : e.CreatedAt >= cutoff;
    }

    public static DateTimeOffset? TryClaim(IReadOnlyList<SessionCaptureEvent> messages, int recentAttempts,
        SessionCaptureOptions options, DateTimeOffset now)
    {
        if (messages.Any(e => e.CaptureExpiresAt is { } deadline ? deadline <= now : e.CreatedAt < now.AddHours(-options.RetentionHours))) return null;
        var leaseUntil = now.AddSeconds(options.ExtractionTimeoutSeconds + 60);
        var token = Guid.NewGuid();
        foreach (var message in messages)
        {
            message.Status = "processing";
            message.LeaseToken = token;
            message.LeaseUntil = leaseUntil;
        }
        return leaseUntil;
    }

    public static void Complete(SessionCaptureEvent message, string? resultCode, SessionCaptureDisposition disposition,
        int maxAttempts, DateTimeOffset now)
    {
        message.Status = disposition switch
        {
            SessionCaptureDisposition.Retry when message.AttemptCount < maxAttempts => "pending",
            SessionCaptureDisposition.Retry or SessionCaptureDisposition.Failed => "failed",
            _ => "completed"
        };
        message.ResultCode = message.Status == "pending" ? null : resultCode;
        message.NextAttemptAt = now.AddMinutes(2);
        message.ProcessedAt = now;
        message.LeaseUntil = null;
        message.LeaseToken = null;
    }
}
