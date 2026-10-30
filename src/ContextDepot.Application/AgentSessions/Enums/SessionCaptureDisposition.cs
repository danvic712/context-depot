namespace ContextDepot.Application.AgentSessions.Enums;

/// <summary>Application-approved outcome of a capture processing attempt.</summary>
public enum SessionCaptureDisposition
{
    Completed,
    Retry,
    Failed
}
