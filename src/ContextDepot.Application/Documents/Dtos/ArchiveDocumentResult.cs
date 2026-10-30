namespace ContextDepot.Application.Documents.Dtos;

/// <summary>Confirms that a document was archived without deleting its canonical file.</summary>
/// <param name="Archived">Whether the archive operation completed.</param>
/// <param name="DocumentId">The archived document identifier.</param>
public sealed record ArchiveDocumentResult(bool Archived, Guid DocumentId);
