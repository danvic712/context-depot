namespace ContextDepot.Application.Documents.Dtos;

/// <summary>
/// A current validation snapshot. Warnings allow targets to be written in a later batch.
/// </summary>
public sealed record DocumentLinkValidation(
    IReadOnlyList<DocumentLinkDiagnostic> Errors,
    IReadOnlyList<DocumentLinkDiagnostic> Warnings)
{
    public bool IsValid => Errors.Count == 0;
    public static DocumentLinkValidation Empty { get; } = new([], []);
}
