using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure;

public sealed class ContextDepotStartupInitializer(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    DatabaseApplicationSettingsReloadService applicationSettingsReloadService,
    InferenceRuntimeSnapshotLoader inferenceRuntimeSnapshotLoader,
    VectorCollectionInitializer vectorCollectionInitializer,
    ILogger<ContextDepotStartupInitializer> logger)
{
    public async Task EnsureDatabaseSchemaAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    throw new InvalidOperationException(
                        "Pending database migrations must be applied before starting ContextDepot in production.");
                }
            }

        }
        catch (Exception exception)
        {
            logger.LogCritical(exception,
                "{ErrorCode} failed while validating database schema, loading application settings or initializing vector collections.",
                ApplicationErrorCodes.DatabaseMigrationFailed);
            throw;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseSchemaAsync(cancellationToken);
        await applicationSettingsReloadService.LoadInitialAsync(cancellationToken);
        await inferenceRuntimeSnapshotLoader.LoadAsync(cancellationToken);
        await vectorCollectionInitializer.InitializeAsync(cancellationToken);
    }
}
