using ContextDepot.Application.Documents;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

/// <summary>
/// Provides trusted Web management operations for canonical documents.
/// </summary>
/// <param name="service">
/// The application service that authorizes and executes permanent document deletion.
/// </param>
[ApiController]
[Route("api/documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DocumentsController(DocumentManagementAppService service) : ControllerBase
{
    /// <summary>
    /// Permanently deletes an active or archived document, its canonical Markdown, chunks and vector indexes.
    /// </summary>
    /// <param name="id">
    /// The document identifier in the selected depot.
    /// </param>
    /// <param name="expectedUpdatedAt">
    /// The exact reader revision supplied in the query string to reject concurrent changes.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing before canonical content is removed.
    /// </param>
    /// <returns>
    /// A task whose result is an empty HTTP 204 response after deletion completes.
    /// </returns>
    /// <remarks>
    /// Requires trusted Web management access; Depot Access Keys cannot perform this operation.
    /// The server resolves the canonical path and finishes commit or compensation after the source is removed.
    /// </remarks>
    /// <response code="204">
    /// The resource and its derived indexes were deleted.
    /// </response>
    /// <response code="400">
    /// The expected revision is missing or invalid.
    /// </response>
    /// <response code="403">
    /// Trusted Web management access is required.
    /// </response>
    /// <response code="404">
    /// The resource or its owning workspace does not exist in the selected depot.
    /// </response>
    /// <response code="409">
    /// The resource revision changed after it was read.
    /// </response>
    /// <response code="500">
    /// Canonical content could not be removed.
    /// </response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, expectedUpdatedAt, cancellationToken);
        return NoContent();
    }
}
