using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContextDepot.HealthChecks;

public static class ReadinessResponseWriter
{
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString().ToLowerInvariant(),
                    entry.Value.Description,
                    data = entry.Value.Data.ToDictionary(item => item.Key, item => item.Value)
                })
        };
        await JsonSerializer.SerializeAsync(context.Response.Body, payload);
    }
}
