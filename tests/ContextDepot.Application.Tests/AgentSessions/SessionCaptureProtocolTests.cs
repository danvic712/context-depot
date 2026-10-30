using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class SessionCaptureProtocolTests
{
    private static SessionCaptureRequest Turn => new(2, "plugin", null, null, null, "completed-1", "project", null,
        "turn", [new("user", "我决定使用 PostgreSQL 数据库。"), new("assistant", "已记录到当前方案。")]);

    [Fact]
    public void Legacy_and_v2_messages_normalize_to_the_same_pairing_and_deduplication_keys()
    {
        var legacy = new SessionCaptureRequest(1, "codex", "session", "turn", "user", "event", "project", "visible");
        var modern = legacy with { SchemaVersion = 2, Phase = null, Content = null, Mode = "message", Messages = [new("user", "visible")] };
        var first = SessionCaptureProtocol.Normalize(legacy);
        var second = SessionCaptureProtocol.Normalize(modern);
        Assert.Equal(first.ClientSessionId, second.ClientSessionId);
        Assert.Equal(first.TurnId, second.TurnId);
        Assert.Equal(first.EventIds, second.EventIds);
        Assert.Equal(first.Messages, second.Messages);
    }

    [Fact]
    public void Standalone_retries_have_stable_bounded_keys_and_different_events_never_pair()
    {
        var request = Turn with { EventId = new string('x', 200) };
        var first = SessionCaptureProtocol.Normalize(request);
        var retry = SessionCaptureProtocol.Normalize(request);
        var other = SessionCaptureProtocol.Normalize(Turn);
        Assert.Equal(first.ClientSessionId, retry.ClientSessionId);
        Assert.Equal(first.TurnId, retry.TurnId);
        Assert.Equal(first.EventIds, retry.EventIds);
        Assert.NotEqual(first.ClientSessionId, other.ClientSessionId);
        Assert.NotEqual(first.TurnId, other.TurnId);
        Assert.All(first.EventIds, id => Assert.InRange(id.Length, 1, 200));
    }

    [Fact]
    public void Native_turn_keeps_outer_session_and_prompt_identity()
    {
        var capture = SessionCaptureProtocol.Normalize(Turn with { ClientSessionId = "forked-session", TurnId = "outer-prompt" });
        Assert.Equal("forked-session", capture.ClientSessionId);
        Assert.Equal("outer-prompt", capture.TurnId);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("mode")]
    [InlineData("legacy-content")]
    [InlineData("legacy-phase")]
    [InlineData("partial-identity")]
    [InlineData("empty-identity")]
    [InlineData("null-messages")]
    [InlineData("null-item")]
    [InlineData("reversed")]
    [InlineData("tool-role")]
    [InlineData("extra-message")]
    [InlineData("empty-turn")]
    [InlineData("oversized")]
    [InlineData("message-without-identity")]
    public void Ambiguous_or_incomplete_submissions_are_rejected(string defect)
    {
        var request = defect switch
        {
            "version" => Turn with { SchemaVersion = 3 },
            "mode" => Turn with { Mode = "batch" },
            "legacy-content" => Turn with { Content = "duplicate source" },
            "legacy-phase" => Turn with { Phase = "user" },
            "partial-identity" => Turn with { ClientSessionId = "session" },
            "empty-identity" => Turn with { ClientSessionId = "", TurnId = "" },
            "null-messages" => Turn with { Messages = null },
            "null-item" => Turn with { Messages = [null!, new("assistant", "visible")] },
            "reversed" => Turn with { Messages = [new("assistant", "visible"), new("user", "visible")] },
            "tool-role" => Turn with { Messages = [new("user", "visible"), new("tool", "payload")] },
            "extra-message" => Turn with { Messages = [.. Turn.Messages!, new("assistant", "another")] },
            "empty-turn" => Turn with { Messages = [new("user", "visible"), new("assistant", " ")] },
            "oversized" => Turn with { Messages = [new("user", new string('x', 20_001)), new("assistant", "visible")] },
            _ => Turn with { Mode = "message", Messages = [new("user", "visible")] }
        };
        var error = Assert.Throws<ContextDepotApplicationException>(() => SessionCaptureProtocol.Normalize(request));
        Assert.Equal(ApplicationErrorCodes.InvalidSessionCapture, error.ErrorCode);
    }

    [Fact]
    public void Legacy_requests_cannot_silently_ignore_modern_message_fields()
    {
        Assert.Throws<ContextDepotApplicationException>(() => SessionCaptureProtocol.Normalize(Turn with
        {
            SchemaVersion = 1, ClientSessionId = "session", TurnId = "turn", Phase = "user", Content = "visible"
        }));
    }
}
