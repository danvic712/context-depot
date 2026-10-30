namespace ContextDepot.Application.Contexts.Dtos;

/// <summary>Confirms that a Context was archived without deleting its stored content.</summary>
/// <param name="Archived">Whether the archive operation completed.</param>
/// <param name="ContextId">The archived Context identifier.</param>
public sealed record ArchiveContextResult(bool Archived, Guid ContextId);
