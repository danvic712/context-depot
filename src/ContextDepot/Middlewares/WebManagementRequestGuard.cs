using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Middlewares;

// The management header prevents cross-site submissions; management still requires a trusted deployment.
public static class WebManagementRequestGuard
{
    public static void RequireManagementRequest(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-ContextDepot-Key") ||
            context.Request.Headers["Sec-Fetch-Site"] == "cross-site" ||
            (!HttpMethods.IsGet(context.Request.Method) && context.Request.Headers["X-ContextDepot-Management"] != "web"))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsForbidden);
    }
}
