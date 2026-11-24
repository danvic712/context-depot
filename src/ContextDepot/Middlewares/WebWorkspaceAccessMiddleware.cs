using ContextDepot.Infrastructure.Access;

namespace ContextDepot.Middlewares;

public sealed class WebWorkspaceAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WorkspaceAccessContext access)
    {
        if (!context.Request.Path.StartsWithSegments("/api/workspaces") &&
            !context.Request.Path.StartsWithSegments("/api/knowledge") &&
            !context.Request.Path.StartsWithSegments("/api/documents") &&
            !context.Request.Path.StartsWithSegments("/api/contexts") &&
            !context.Request.Path.StartsWithSegments("/api/settings"))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/settings") ||
            context.Request.Path.StartsWithSegments("/api/documents") || context.Request.Path.StartsWithSegments("/api/contexts"))
            WebManagementRequestGuard.RequireManagementRequest(context);

        // Appearance is a deployment default.
        if (context.Request.Path.StartsWithSegments("/api/settings/appearance"))
        {
            await next(context);
            return;
        }

        access.AllowInternalAccess();
        await next(context);
    }
}
