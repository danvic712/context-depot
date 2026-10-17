namespace ContextDepot.Application.Documents.Dtos;

/// <summary>
/// Identifies one link at a one-based source position. Codes are stable and client-neutral.
/// </summary>
public sealed record DocumentLinkDiagnostic(
    string Code,
    string Href,
    int Line,
    int Column,
    string? TargetPath = null,
    string? Fragment = null);
