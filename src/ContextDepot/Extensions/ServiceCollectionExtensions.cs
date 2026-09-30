namespace ContextDepot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IEndpointConventionBuilder MapContextDepot(
        this IEndpointRouteBuilder endpoints)
    {
        var fallback = endpoints.MapFallbackToFile("index.html");
        fallback.Add(endpoint =>
        {
            var serveIndex = endpoint.RequestDelegate!;
            endpoint.RequestDelegate = context => IsServerPath(context.Request.Path)
                ? Results.NotFound().ExecuteAsync(context)
                : serveIndex(context);
        });
        return fallback;
    }

    private static bool IsServerPath(PathString path) =>
        path.StartsWithSegments("/api") ||
        path.StartsWithSegments("/mcp") ||
        path.StartsWithSegments("/healthz") ||
        path.StartsWithSegments("/readyz");
}
