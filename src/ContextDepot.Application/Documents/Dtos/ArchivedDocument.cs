namespace ContextDepot.Application.Documents.Dtos;

/// <summary>
/// Describes an archived canonical document and the revision required for permanent deletion.
/// </summary>
/// <param name="Id">
/// The archived document identifier.
/// </param>
/// <param name="Title">
/// The saved document title.
/// </param>
/// <param name="Path">
/// The document path relative to its owning workspace.
/// </param>
/// <param name="UpdatedAt">
/// The precise observed revision required for revision-checked deletion.
/// </param>
public sealed record ArchivedDocument(Guid Id, string Title, string Path, DateTimeOffset UpdatedAt);
