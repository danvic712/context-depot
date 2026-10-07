using ContextDepot.Application.Setup.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Setup;

namespace ContextDepot.Middlewares;

public sealed class InstallationSetupMiddleware(RequestDelegate next)
{
    private int completed;

    public async Task InvokeAsync(HttpContext context, ISetupAppService setup)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/setup"))
        {
            WebManagementRequestGuard.RequireManagementRequest(context);
            await next(context);
            return;
        }

        if (path.StartsWithSegments("/api/settings/appearance") ||
            (!path.StartsWithSegments("/api/settings") && !path.StartsWithSegments("/api/workspaces") &&
             !path.StartsWithSegments("/api/knowledge") && !path.StartsWithSegments("/mcp")))
        {
            await next(context);
            return;
        }

        // Completion is permanent for this installation, so ordinary requests need no further setup reads.
        if (Volatile.Read(ref completed) == 0)
        {
            var status = await setup.GetAsync(context.RequestAborted);
            if (status.State == InstallationState.Completed)
                Interlocked.Exchange(ref completed, 1);
            else
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SetupRequired);
        }

        await next(context);
    }
}
