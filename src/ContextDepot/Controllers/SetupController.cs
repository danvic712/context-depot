using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Setup.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/setup")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SetupController(ISetupAppService setup) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SetupStatusDto>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await setup.GetAsync(cancellationToken));

    [HttpGet("inference")]
    public async Task<ActionResult<InferenceProviderSettingsDto>> GetInferenceAsync(CancellationToken cancellationToken) =>
        Ok(await setup.GetInferenceSettingsAsync(cancellationToken));

    [HttpPost("complete")]
    public async Task<ActionResult<SetupCompletionDto>> CompleteAsync(CompleteSetupRequest request, CancellationToken cancellationToken) =>
        Ok(await setup.CompleteAsync(request, cancellationToken));
}
