using ContextDepot.Application.AgentSessions.Enums;

namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record CaptureProcessingResult(SessionCaptureDisposition Disposition, string? ResultCode = null,
    string? LastErrorCode = null, DateTimeOffset? NotBefore = null);
