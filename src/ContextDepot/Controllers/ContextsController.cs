using ContextDepot.Application.Contexts;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

/// <summary>
/// Provides trusted Web management operations for structured contexts.
/// </summary>
/// <param name="service">
/// The application service that authorizes and executes permanent context deletion.
/// </param>
[ApiController]
[Route("api/contexts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContextsController(ContextManagementAppService service) : ControllerBase
{
    /// <summary>
    /// Permanently deletes a context and every associated vector index.
    /// </summary>
    /// <param name="id">
    /// The context identifier in the selected depot.
    /// </param>
    /// <param name="expectedUpdatedAt">
    /// The exact reader revision supplied in the query string to reject concurrent changes.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels request processing before commit.
    /// </param>
    /// <returns>
    /// A task whose result is an empty HTTP 204 response after deletion completes.
    /// </returns>
    /// <remarks>
    /// Requires trusted Web management access; Depot Access Keys cannot perform this operation.
    /// Successor contexts are preserved and their reference to the deleted context is cleared.
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
    /// The context does not exist in the selected depot.
    /// </response>
    /// <response code="409">
    /// The resource revision changed after it was read.
    /// </response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, expectedUpdatedAt, cancellationToken);
        return NoContent();
    }
}
