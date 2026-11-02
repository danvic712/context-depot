using System.ComponentModel;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ModelContextProtocol.Server;

namespace ContextDepot.MCP.Tools;

public sealed class SessionTools(MCPToolExecutor executor, ILogger<SessionTools> logger)
{
    [McpServerTool(Name = "session_capture", Title = "Capture visible session memory", Destructive = false,
        Idempotent = true, UseStructuredContent = true)]
    [Description("Submit visible dialogue only. workspaceMode defaults to explicit and requires workspace; auto requires schema 2 and omits workspace/sessionId, routes only this turn's user input and may abstain. Server may save evidence-backed summaries of durable user information; unconfirmed assistant claims are not evidence. Supply sessionId from session_start for lifecycle-managed capture; agent, clientSessionId and Workspace must match. No session is created or recalled here. Legacy unbound capture remains supported. Schema 2: mode=message requires clientSessionId, turnId and one messages item (role=user or assistant); mode=turn requires exactly [user, assistant], both nonempty, and allows omitting both native IDs. Each content is at most 20000 characters. Keep eventId stable on retry. Submit a completed visible exchange only when invoking manually. Schema 1 phase/content remains supported; do not mix it with mode/messages. Server opt-in and the current key grant are required. accepted confirms queue persistence only, not saved memory. Never send hidden instructions, reasoning, tools, attachments or transcripts. Agent/role are unverified labels. Do not also automatically context_save the same turn.")]
    public Task<SessionCaptureReceipt> CaptureAsync(ISessionCaptureAppService service,
        int schemaVersion, string agent, string eventId, string? workspace = null,
        string? clientSessionId = null, string? turnId = null, string? phase = null, string? content = null,
        string? mode = null, IReadOnlyList<SessionCaptureMessage>? messages = null, Guid? sessionId = null,
        [Description("explicit (default) requires workspace; auto requires schema 2 and omits workspace/sessionId. Auto routes this turn's user text to an existing authorized Workspace, or abstains.")] string? workspaceMode = null,
        CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(() => service.CaptureAsync(new(schemaVersion, agent, clientSessionId, turnId,
            phase, eventId, workspace, content, mode, messages, sessionId, workspaceMode), cancellationToken), logger);

    [McpServerTool(Name = "session_capture_status", Title = "Read capture receipt status", ReadOnly = true,
        Idempotent = true, UseStructuredContent = true)]
    [Description("Read a capture without creating a session or triggering processing. Supply exactly one of receiptId or turnReceiptId. Only the original authenticated key can query receipts; target Workspace authorization must remain valid. Reports routing, processing and saved Context IDs, never dialogue, evidence or model output. accepted is not saved; use context_get on saved IDs to verify summaries and evidence.")]
    public Task<SessionCaptureStatusResult> CaptureStatusAsync(ISessionCaptureStatusAppService service,
        Guid? receiptId = null, Guid? turnReceiptId = null, CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(() => service.GetAsync(new(receiptId, turnReceiptId), cancellationToken), logger);

    [McpServerTool(Name = "session_start", Title = "Start a memory session", Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Create or retrieve a session bound to the current key and one exact existing Workspace. Stable agent/clientSessionId identify retries. Does not capture text, recall knowledge, or reopen an ended session.")]
    public Task<SessionStartResult> StartAsync(ISessionAppService service, string agent, string clientSessionId,
        string workspace, CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(() => service.StartAsync(new(agent, clientSessionId, workspace), cancellationToken), logger);

    [McpServerTool(Name = "session_end", Title = "End a memory session", Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Close a session to new captures. Existing accepted events continue asynchronous processing. Does not capture a final message, force extraction, delete knowledge or end the client process. Reasons: completed, interrupted, other. Identical retries retain the original end time and reason.")]
    public Task<SessionModel> EndAsync(ISessionAppService service, Guid sessionId, string reason = "completed",
        CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(() => service.EndAsync(sessionId, reason, cancellationToken), logger);

    [McpServerTool(Name = "session_recall", Title = "Recall session-related knowledge", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Read existing knowledge relevant to query inside the session's authorized Workspace, using the shared bootstrap retrieval implementation. Does not start or mutate a session, capture input or inject context into any client. Ended sessions remain readable while authorized.")]
    public Task<SessionRecallResult> RecallAsync(ISessionAppService service, Guid sessionId, string query,
        int maxTokens = 1200, CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(() => service.RecallAsync(sessionId, query, maxTokens, cancellationToken), logger);

    [McpServerTool(Name = "session_status", Title = "Read session processing status", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Read lifecycle metadata and event processing counts for the current key's session. Optional receiptId returns one matching capture's diagnostic state. Never returns dialogue or secrets and never triggers processing. Ended does not imply extraction finished; saved turns do not count Context items.")]
    public Task<SessionStatusResult> StatusAsync(ISessionAppService service, Guid sessionId, Guid? receiptId = null,
        CancellationToken cancellationToken = default) =>
        executor.ExecuteAsync(() => service.StatusAsync(sessionId, receiptId, cancellationToken), logger);
}
