using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class SessionCapturePolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T10:00:00Z");
    private static readonly SessionCaptureOptions Options = new();

    [Theory]
    [InlineData(false, "skipped_incomplete")]
    [InlineData(true, "skipped_expired")]
    public void Expiry_distinguishes_missing_partner_and_complete_unprocessed_turn(bool hasPartner, string resultCode)
    {
        var message = new SessionCaptureEvent { CreatedAt = Now.AddHours(-25), EncryptedContent = "encrypted" };
        Assert.True(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        SessionCapturePolicy.Cleanup(message, hasPartner, Options, Now);
        Assert.Equal("completed", message.Status);
        Assert.Equal(resultCode, message.ResultCode);
        Assert.Null(message.EncryptedContent);
        Assert.Equal(Now, message.ProcessedAt);
    }

    [Fact]
    public void Claims_do_not_consume_or_enforce_the_shared_provider_budget()
    {
        var user = new SessionCaptureEvent { CreatedAt = Now };
        var assistant = new SessionCaptureEvent { Phase = "assistant", CreatedAt = Now };
        var lease = SessionCapturePolicy.TryClaim([user, assistant], Options.MaxCallsPerHour, Options, Now);
        Assert.True(lease > Now);
        Assert.Equal("processing", user.Status);
        Assert.Equal("processing", assistant.Status);
        Assert.Equal(0, user.AttemptCount);
        Assert.Equal(0, assistant.AttemptCount);
        Assert.NotNull(user.LeaseToken);
        Assert.Equal(user.LeaseToken, assistant.LeaseToken);
    }

    [Fact]
    public void Claiming_a_pair_assigns_the_same_unique_token_without_consuming_provider_attempts()
    {
        var user = new SessionCaptureEvent { CreatedAt = Now };
        var assistant = new SessionCaptureEvent { Phase = "assistant", CreatedAt = Now };
        var lease = SessionCapturePolicy.TryClaim([user, assistant], 0, Options, Now);
        Assert.True(lease > Now);
        Assert.Equal(lease, user.LeaseUntil);
        Assert.Equal(lease, assistant.LeaseUntil);
        Assert.Equal("processing", user.Status);
        Assert.Equal("processing", assistant.Status);
        Assert.Equal(0, user.AttemptCount);
        Assert.Equal(0, assistant.AttemptCount);
        Assert.NotNull(user.LeaseToken);
        Assert.Equal(user.LeaseToken, assistant.LeaseToken);
        var oldToken = user.LeaseToken;
        SessionCapturePolicy.TryClaim([user, assistant], 0, Options, Now.AddMinutes(3));
        Assert.NotEqual(oldToken, user.LeaseToken);
        Assert.Equal(user.LeaseToken, assistant.LeaseToken);
    }

    [Fact]
    public void An_unexpired_turn_keeps_its_body_and_remains_pending()
    {
        var message = new SessionCaptureEvent { CreatedAt = Now, EncryptedContent = "encrypted" };
        Assert.False(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        SessionCapturePolicy.Cleanup(message, false, Options, Now);
        Assert.Equal("pending", message.Status);
        Assert.Equal("encrypted", message.EncryptedContent);
        Assert.Null(message.ProcessedAt);
    }

    [Theory]
    [InlineData(SessionCaptureDisposition.Completed, 1, "completed")]
    [InlineData(SessionCaptureDisposition.Retry, 1, "pending")]
    [InlineData(SessionCaptureDisposition.Retry, 3, "failed")]
    [InlineData(SessionCaptureDisposition.Failed, 1, "failed")]
    public void Completion_respects_approved_disposition_and_attempt_limit(
        SessionCaptureDisposition disposition, int attemptCount, string expectedStatus)
    {
        var message = new SessionCaptureEvent { Status = "processing", AttemptCount = attemptCount, LeaseUntil = Now.AddMinutes(1) };
        SessionCapturePolicy.Complete(message, "result", disposition, 3, Now);
        Assert.Equal(expectedStatus, message.Status);
        Assert.Equal(expectedStatus == "pending" ? null : "result", message.ResultCode);
        Assert.Null(message.LeaseToken);
        Assert.Equal(Now, message.ProcessedAt);
        Assert.Null(message.LeaseUntil);
        if (disposition == SessionCaptureDisposition.Retry && attemptCount < 3)
        {
            Assert.True(message.NextAttemptAt > Now);
        }
    }

    [Fact]
    public void An_exhausted_abandoned_lease_becomes_failed_without_losing_diagnostic_metadata()
    {
        var message = new SessionCaptureEvent
        {
            Status = "processing", CreatedAt = Now, AttemptCount = 3, LeaseUntil = Now.AddMinutes(-1), EncryptedContent = "encrypted"
        };
        Assert.True(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        SessionCapturePolicy.Cleanup(message, true, Options, Now);
        Assert.Equal("failed", message.Status);
        Assert.Equal("retry_exhausted", message.ResultCode);
        Assert.Equal("encrypted", message.EncryptedContent);
        Assert.Null(message.LeaseUntil);
    }

    [Fact]
    public void Terminal_retention_purges_body_and_preserves_receipt_result()
    {
        var message = new SessionCaptureEvent
        {
            Status = "completed", ResultCode = "saved", ProcessedAt = Now.AddHours(-25), EncryptedContent = "encrypted"
        };
        SessionCapturePolicy.Cleanup(message, true, Options, Now);
        Assert.Null(message.EncryptedContent);
        Assert.Equal("saved", message.ResultCode);
        Assert.Equal("completed", message.Status);
    }

    [Fact]
    public void A_late_partner_cannot_reactivate_an_expired_turn()
    {
        var message = new SessionCaptureEvent { EncryptedContent = "encrypted" };
        SessionCapturePolicy.ApplyLateArrival(message, true, Now);
        Assert.Equal("completed", message.Status);
        Assert.Equal("skipped_incomplete", message.ResultCode);
        Assert.Null(message.EncryptedContent);
    }
    [Fact]
    public void Automatic_deadline_purges_completed_content_without_extending_from_processed_at()
    {
        var message = new SessionCaptureEvent
        {
            Status = "completed", ResultCode = "saved", CreatedAt = Now.AddHours(-24),
            CaptureExpiresAt = Now, ProcessedAt = Now, EncryptedContent = "encrypted",
            EncryptedExtractionPlan = "plan", ExtractionPlanHash = new string('A', 64)
        };
        Assert.True(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        SessionCapturePolicy.Cleanup(message, true, Options, Now);
        Assert.Null(message.EncryptedContent);
        Assert.Null(message.EncryptedExtractionPlan);
        Assert.Null(message.ExtractionPlanHash);
        Assert.Equal(Now, message.ContentPurgedAt);
        Assert.Equal("saved", message.ResultCode);
        Assert.False(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
    }

    [Fact]
    public void Plan_recovery_uses_persistence_limit_instead_of_model_attempt_limit()
    {
        var message = new SessionCaptureEvent
        {
            Status = "processing", CreatedAt = Now, LeaseUntil = Now.AddSeconds(-1),
            AttemptCount = Options.MaxAttempts, EncryptedExtractionPlan = "plan"
        };
        Assert.False(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        message.PersistenceAttemptCount = Options.PersistenceMaxAttempts;
        Assert.True(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        SessionCapturePolicy.Cleanup(message, true, Options, Now);
        Assert.Equal("failed", message.Status);
        Assert.Equal("retry_exhausted", message.ResultCode);
    }

    [Fact]
    public void Automatic_expiry_prevents_claim_even_for_a_recent_record()
    {
        var message = new SessionCaptureEvent { CreatedAt = Now, CaptureExpiresAt = Now };
        Assert.False(SessionCapturePolicy.WithinRetention(Options, Now).Compile()(message));
        Assert.Null(SessionCapturePolicy.TryClaim([message], 0, Options, Now));
        Assert.Null(message.LeaseToken);
    }

}
