using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.CurrentOwner;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure;

internal sealed class ContextDepotInfrastructureInitializer(
    IServiceScopeFactory scopeFactory,
    VectorCollectionInitializer vectorCollectionInitializer,
    ILogger<ContextDepotInfrastructureInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            await vectorCollectionInitializer.InitializeAsync(cancellationToken);
            await scope.ServiceProvider
                .GetRequiredService<CurrentOwnerBootstrapper>()
                .InitializeAsync(cancellationToken);
        }
        catch (ContextDepotApplicationException exception) when (exception.ErrorCode == ApplicationErrorCodes.OwnerConfigurationMismatch)
        {
            logger.LogCritical(exception, "{ErrorCode} detected while initializing the current owner.", ApplicationErrorCodes.OwnerConfigurationMismatch);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "{ErrorCode} failed while applying migrations or initializing the current owner.", ApplicationErrorCodes.DatabaseMigrationFailed);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
