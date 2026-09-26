using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure;

public sealed class ContextDepotStartupInitializer(
    IServiceScopeFactory scopeFactory,
    DatabaseApplicationSettingsSnapshotLoader applicationSettingsSnapshotLoader,
    InferenceRuntimeSnapshotRefresher inferenceRuntimeSnapshotRefresher,
    ILogger<ContextDepotStartupInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await EnsureDatabaseSchemaAsync(cancellationToken);
        await applicationSettingsSnapshotLoader.RefreshAsync(cancellationToken);
        await inferenceRuntimeSnapshotRefresher.RefreshAsync(cancellationToken);
    }

    private async Task EnsureDatabaseSchemaAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception,
                "{ErrorCode} failed while validating database schema, loading application settings or initializing vector collections.",
                ApplicationErrorCodes.DatabaseMigrationFailed);
            throw;
        }
    }
}
