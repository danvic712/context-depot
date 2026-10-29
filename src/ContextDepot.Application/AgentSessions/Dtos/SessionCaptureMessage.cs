namespace ContextDepot.Application.AgentSessions.Dtos;

/// <summary>A client-declared visible message, never a transcript or tool payload.</summary>
public sealed record SessionCaptureMessage(string Role, string? Content);
