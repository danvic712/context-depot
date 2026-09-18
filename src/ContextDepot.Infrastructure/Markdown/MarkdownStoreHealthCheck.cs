using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ContextDepot.Infrastructure.Markdown;

public sealed class MarkdownStoreHealthCheck(FileSystemMarkdownStore store, IOptions<MarkdownStoreOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.CanReadAndWrite()
            ? HealthCheckResult.Healthy($"Markdown root is available: {options.Value.Root}")
            : HealthCheckResult.Unhealthy("Markdown root is not readable and writable."));
    }
}
