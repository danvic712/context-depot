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
    public void Hourly_budget_defers_the_pair_without_consuming_an_attempt()
    {
        var user = new SessionCaptureEvent();
        var assistant = new SessionCaptureEvent { Phase = "assistant" };
        var lease = SessionCapturePolicy.TryClaim([user, assistant], Options.MaxCallsPerHour, Options, Now);
        Assert.Null(lease);
        Assert.Equal("pending", user.Status);
        Assert.Equal("pending", assistant.Status);
        Assert.Equal(0, user.AttemptCount);
        Assert.Equal(0, assistant.AttemptCount);
        Assert.Equal(user.NextAttemptAt, assistant.NextAttemptAt);
        Assert.True(user.NextAttemptAt > Now);
    }

    [Fact]
    public void Claiming_a_pair_assigns_the_same_lease_and_increments_each_attempt()
    {
        var user = new SessionCaptureEvent();
        var assistant = new SessionCaptureEvent { Phase = "assistant" };
        var lease = SessionCapturePolicy.TryClaim([user, assistant], 0, Options, Now);
        Assert.True(lease > Now);
        Assert.Equal(lease, user.LeaseUntil);
        Assert.Equal(lease, assistant.LeaseUntil);
        Assert.Equal("processing", user.Status);
        Assert.Equal("processing", assistant.Status);
        Assert.Equal(1, user.AttemptCount);
        Assert.Equal(1, assistant.AttemptCount);
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
        Assert.Equal("result", message.ResultCode);
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
            Status = "processing", AttemptCount = 3, LeaseUntil = Now.AddMinutes(-1), EncryptedContent = "encrypted"
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
}
