using ContextDepot.Application.Depots.Contracts;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Middlewares;

// This iteration has no Web permission model. The host selects one Depot; callers cannot select it.
public sealed class WebDepotContextMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context, IDepotRepository depots, CurrentDepotAccessContext currentDepot)
    {
        if (!context.Request.Path.StartsWithSegments("/api/workspaces") &&
            !context.Request.Path.StartsWithSegments("/api/knowledge"))
        {
            await next(context);
            return;
        }

        var configuredId = configuration["ContextDepot:Web:DepotId"];
        var candidates = await depots.ListAsync(context.RequestAborted);
        var selected = string.IsNullOrWhiteSpace(configuredId)
            ? (candidates.Count == 1 ? candidates[0] : null)
            : (Guid.TryParse(configuredId, out var id) ? candidates.SingleOrDefault(x => x.Id == id) : null);
        if (selected is null)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.WebDepotUnavailable);
        }

        currentDepot.InitializeForWeb(selected.Id, selected.DisplayName);
        await next(context);
    }
}
