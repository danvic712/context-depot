using ContextDepot.Infrastructure.Contracts;
using ContextDepot.Infrastructure.CurrentDepot;

namespace ContextDepot.Middlewares;

public sealed class DepotAccessKeyAuthenticationMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-ContextDepot-Key";

    public async Task InvokeAsync(
        HttpContext httpContext,
        IDepotAccessKeyAuthenticator authenticator,
        CurrentDepotAccessContext accessContext)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/mcp"))
        {
            await next(httpContext);
            return;
        }

        var values = httpContext.Request.Headers[HeaderName];
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            await WriteUnauthorizedAsync(httpContext);
            return;
        }

        var identity = await authenticator.AuthenticateAsync(values[0]!, httpContext.RequestAborted);
        if (identity is null)
        {
            await WriteUnauthorizedAsync(httpContext);
            return;
        }

        accessContext.Initialize(identity);
        await next(httpContext);
    }

    private static Task WriteUnauthorizedAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.Headers.WWWAuthenticate = "ContextDepotKey";
        return httpContext.Response.WriteAsJsonAsync(new
        {
            title = "Unauthorized",
            status = StatusCodes.Status401Unauthorized
        });
    }
}
