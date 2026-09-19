using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.Markdown;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class MarkdownStoreHealthCheck(FileSystemMarkdownStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(store.CanReadAndWrite()
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                ApplicationErrorMessages.Get(ApplicationErrorCodes.MarkdownRootUnavailable)));
    }
}
