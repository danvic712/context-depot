using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class CurrentOwnerHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
        var ownerContext = scope.ServiceProvider.GetRequiredService<ICurrentOwnerContext>();
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy(
                ApplicationErrorMessages.Get(ApplicationErrorCodes.DatabaseUnavailable));
        }

        var owners = await db.Owners.AsNoTracking().ToListAsync(cancellationToken);
        return owners.Count == 1 && owners[0].Id == ownerContext.OwnerId
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                ApplicationErrorMessages.Get(ApplicationErrorCodes.OwnerConfigurationMismatch));
    }
}
