using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/settings/appearance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AppearanceController(IAppearanceSettingsAppService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AppearanceSettingsDto>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(cancellationToken));

    [HttpPut("theme")]
    public async Task<ActionResult<ThemeRequest>> SetThemeAsync(
        ThemeRequest request, CancellationToken cancellationToken)
    {
        await service.SetThemeAsync(request, cancellationToken);
        return Ok(request);
    }

    [HttpPut("language")]
    public async Task<ActionResult<LanguageRequest>> SetLanguageAsync(
        LanguageRequest request, CancellationToken cancellationToken)
    {
        await service.SetLanguageAsync(request, cancellationToken);
        return Ok(request);
    }
}
