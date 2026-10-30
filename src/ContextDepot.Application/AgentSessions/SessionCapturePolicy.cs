using System.Linq.Expressions;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.AgentSessions;

/// <summary>Capture lifecycle decisions shared by atomic repository operations.</summary>
public static class SessionCapturePolicy
{
    public static void ApplyLateArrival(SessionCaptureEvent message, bool hasTerminalPartner, DateTimeOffset now)
    {
        if (!hasTerminalPartner)
        {
            return;
        }

        Complete(message, "skipped_incomplete", SessionCaptureDisposition.Completed, 0, now);
        message.EncryptedContent = null;
    }

    public static Expression<Func<SessionCaptureEvent, bool>> RequiresCleanup(
        SessionCaptureOptions options, DateTimeOffset now)
    {
        var expired = now.AddHours(-options.RetentionHours);
        return message =>
            (message.Status == "pending" && message.CreatedAt < expired) ||
            ((message.Status == "completed" || message.Status == "failed") &&
                message.ProcessedAt < expired && message.EncryptedContent != null) ||
            (message.Status == "processing" && message.LeaseUntil < now && message.AttemptCount >= options.MaxAttempts);
    }

    public static void Cleanup(SessionCaptureEvent message, bool hasPartner,
        SessionCaptureOptions options, DateTimeOffset now)
    {
        var expired = now.AddHours(-options.RetentionHours);
        if (message.Status == "pending" && message.CreatedAt < expired)
        {
            Complete(message, hasPartner ? "skipped_expired" : "skipped_incomplete", SessionCaptureDisposition.Completed, options.MaxAttempts, now);
            message.EncryptedContent = null;
            return;
        }

        if (message.Status == "processing" && message.LeaseUntil < now && message.AttemptCount >= options.MaxAttempts)
        {
            Complete(message, "retry_exhausted", SessionCaptureDisposition.Failed, options.MaxAttempts, now);
            return;
        }

        if ((message.Status == "completed" || message.Status == "failed") && message.ProcessedAt < expired)
        {
            message.EncryptedContent = null;
        }
    }

    public static DateTimeOffset? TryClaim(IReadOnlyList<SessionCaptureEvent> messages, int recentAttempts,
        SessionCaptureOptions options, DateTimeOffset now)
    {
        if (recentAttempts >= options.MaxCallsPerHour)
        {
            foreach (var message in messages)
            {
                message.NextAttemptAt = now.AddMinutes(5);
            }

            return null;
        }

        var leaseUntil = now.AddSeconds(options.ExtractionTimeoutSeconds + 60);
        foreach (var message in messages)
        {
            message.Status = "processing";
            message.AttemptCount++;
            message.LeaseUntil = leaseUntil;
        }

        return leaseUntil;
    }

    public static void Complete(SessionCaptureEvent message, string resultCode, SessionCaptureDisposition disposition,
        int maxAttempts, DateTimeOffset now)
    {
        message.Status = disposition switch
        {
            SessionCaptureDisposition.Retry when message.AttemptCount < maxAttempts => "pending",
            SessionCaptureDisposition.Retry or SessionCaptureDisposition.Failed => "failed",
            _ => "completed"
        };
        message.ResultCode = resultCode;
        message.NextAttemptAt = now.AddMinutes(2);
        message.ProcessedAt = now;
        message.LeaseUntil = null;
    }
}
