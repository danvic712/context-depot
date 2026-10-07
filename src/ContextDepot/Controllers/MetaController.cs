using System.Reflection;
using ContextDepot.Application.Meta.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Controllers;

[ApiController]
[Route("api/meta")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MetaController : ControllerBase
{
    private static readonly MetaDto Metadata = new(typeof(MetaController).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    [HttpGet]
    public ActionResult<MetaDto> Get() => Ok(Metadata);
}
