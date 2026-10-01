using ContextDepot.Infrastructure.Contracts;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Application.Shared.Exceptions;

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
            throw new ContextDepotApplicationException(ApplicationErrorCodes.Unauthorized);
        }

        var identity = await authenticator.AuthenticateAsync(values[0]!, httpContext.RequestAborted);
        if (identity is null)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.Unauthorized);
        }

        accessContext.Initialize(identity);
        await next(httpContext);
    }

}
