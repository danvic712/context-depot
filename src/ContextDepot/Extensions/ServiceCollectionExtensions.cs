using ContextDepot.Application.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ContextDepot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContextDepotErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
                throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidRequest,
                    context.ModelState.Where(pair => pair.Value?.Errors.Count > 0)
                        .ToDictionary(pair => pair.Key, _ => new[] { ApplicationErrorCodes.InvalidRequest }));
        });
        return services;
    }

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
