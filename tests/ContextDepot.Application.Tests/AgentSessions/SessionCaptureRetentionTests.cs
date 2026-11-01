using ContextDepot.Application.AgentSessions;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class SessionCaptureRetentionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T10:00:00Z");
    private static readonly SessionCaptureOptions Options = new();

    [Fact]
    public void Claim_eligibility_excludes_expired_pairs_beyond_the_cleanup_batch()
    {
        var backlog = Enumerable.Range(0, 300).Select(index => new SessionCaptureEvent
        {
            CreatedAt = Now.AddHours(-25), Phase = index % 2 == 0 ? "user" : "assistant",
            TurnId = (index / 2).ToString(), EncryptedContent = "encrypted"
        }).ToArray();
        foreach (var message in backlog.Take(256))
            SessionCapturePolicy.Cleanup(message, true, Options, Now);

        Assert.Equal(44, backlog.Count(message => message.Status == "pending"));
        var eligible = backlog.AsQueryable().Where(SessionCapturePolicy.WithinRetention(Options, Now))
            .Where(message => message.Status == "pending").ToArray();
        Assert.Empty(eligible);
        Assert.Null(SessionCapturePolicy.TryClaim(backlog.Skip(256).Take(2).ToArray(), 0, Options, Now));
        Assert.All(backlog.Skip(256), message =>
        {
            Assert.Equal("pending", message.Status);
            Assert.Equal(0, message.AttemptCount);
            Assert.Null(message.LeaseUntil);
        });
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    public void Either_expired_partner_prevents_a_new_lease_without_mutating_the_pair(string expiredRole)
    {
        var pair = new[] { "user", "assistant" }.Select(role => new SessionCaptureEvent
        {
            Phase = role, CreatedAt = role == expiredRole ? Now.AddHours(-25) : Now,
            EncryptedContent = "encrypted"
        }).ToArray();

        Assert.Null(SessionCapturePolicy.TryClaim(pair, 0, Options, Now));
        Assert.All(pair, message =>
        {
            Assert.Equal("pending", message.Status);
            Assert.Equal(0, message.AttemptCount);
            Assert.Null(message.LeaseUntil);
            Assert.Equal("encrypted", message.EncryptedContent);
        });
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Retention_cutoff_agrees_between_query_and_claim(long offsetTicks, bool expected)
    {
        var createdAt = Now.AddHours(-Options.RetentionHours).AddTicks(offsetTicks);
        var pair = new[] { new SessionCaptureEvent { CreatedAt = createdAt }, new SessionCaptureEvent { CreatedAt = createdAt } };
        Assert.Equal(expected, SessionCapturePolicy.WithinRetention(Options, Now).Compile()(pair[0]));
        Assert.Equal(expected, SessionCapturePolicy.TryClaim(pair, 0, Options, Now).HasValue);
    }

    [Fact]
    public void An_expired_abandoned_lease_cannot_start_another_extraction()
    {
        var pair = Enumerable.Range(0, 2).Select(_ => new SessionCaptureEvent
        {
            Status = "processing", CreatedAt = Now.AddHours(-25), LeaseUntil = Now.AddMinutes(-1),
            AttemptCount = 1, EncryptedContent = "encrypted"
        }).ToArray();
        Assert.Null(SessionCapturePolicy.TryClaim(pair, 0, Options, Now));
        Assert.All(pair, message => Assert.Equal(1, message.AttemptCount));
        foreach (var message in pair)
        {
            Assert.True(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
            SessionCapturePolicy.Cleanup(message, true, Options, Now);
            Assert.Equal("completed", message.Status);
            Assert.Equal("skipped_expired", message.ResultCode);
            Assert.Null(message.LeaseUntil);
            Assert.Null(message.EncryptedContent);
        }
    }

    [Fact]
    public void Retention_does_not_interrupt_an_already_held_valid_lease()
    {
        var message = new SessionCaptureEvent
        {
            Status = "processing", CreatedAt = Now.AddHours(-25), LeaseUntil = Now.AddMinutes(1),
            AttemptCount = 1, EncryptedContent = "encrypted"
        };
        Assert.False(SessionCapturePolicy.RequiresCleanup(Options, Now).Compile()(message));
        SessionCapturePolicy.Cleanup(message, true, Options, Now);
        Assert.Equal("processing", message.Status);
        Assert.Equal(Now.AddMinutes(1), message.LeaseUntil);
        Assert.Equal("encrypted", message.EncryptedContent);
        Assert.Equal(1, message.AttemptCount);
    }
}
