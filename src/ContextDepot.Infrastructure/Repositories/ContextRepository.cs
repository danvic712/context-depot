using System.Data;
using System.Data.Common;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class ContextRepository(ContextDepotDbContext db) : IContextRepository
{
    public Task<ContextItem?> GetByIdAsync(Guid ownerId, Guid contextId, CancellationToken cancellationToken) =>
        db.ContextItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == contextId, cancellationToken);

    public async Task<ContextPersistenceResult> SaveKeyedAsync(ContextItem candidate, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(candidate.Key))
        {
            throw new ArgumentException(ApplicationErrorMessages.Get(ApplicationErrorCodes.InvalidStateKey), nameof(candidate));
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await SaveKeyedAttemptAsync(candidate, now, cancellationToken);
            if (result.Outcome != ContextPersistenceOutcome.ConcurrencyConflict || attempt == 1)
            {
                return result;
            }
        }

        throw new InvalidOperationException(ApplicationErrorMessages.Get(ApplicationErrorCodes.InternalError));
    }

    private async Task<ContextPersistenceResult> SaveKeyedAttemptAsync(ContextItem candidate, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await db.ContextItems.SingleOrDefaultAsync(x =>
                    x.OwnerId == candidate.OwnerId && x.WorkspaceId == candidate.WorkspaceId &&
                    x.Key == candidate.Key && x.Status == ContextStatus.Active,
                cancellationToken);
            if (current is not null && current.Kind != candidate.Kind)
            {
                return new ContextPersistenceResult(current, ContextPersistenceOutcome.KindConflict);
            }

            if (current is not null && string.Equals(current.Content, candidate.Content, StringComparison.Ordinal))
            {
                return new ContextPersistenceResult(current, ContextPersistenceOutcome.ReusedExisting, current.Id);
            }

            if (current is not null)
            {
                current.MarkSuperseded(now);
                candidate.SetSupersedes(current.Id);
            }

            db.ContextItems.Add(candidate);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContextPersistenceResult(candidate,
                current is null ? ContextPersistenceOutcome.Created : ContextPersistenceOutcome.Replaced, current?.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DetachPendingEntries();
            return new ContextPersistenceResult(candidate, ContextPersistenceOutcome.ConcurrencyConflict);
        }
    }

    private void DetachPendingEntries()
    {
        foreach (var entry in db.ChangeTracker.Entries()
                     .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<ContextPersistenceResult> SaveUnkeyedAsync(ContextItem candidate,
        UnkeyedDuplicatePolicy duplicatePolicy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await LockWorkspaceAsync(candidate.OwnerId, candidate.WorkspaceId, cancellationToken);
            ContextItem? duplicate = duplicatePolicy switch
            {
                UnkeyedDuplicatePolicy.ExactContent => await db.ContextItems.SingleOrDefaultAsync(x =>
                        x.OwnerId == candidate.OwnerId && x.WorkspaceId == candidate.WorkspaceId &&
                        x.Kind == candidate.Kind && x.Status == ContextStatus.Active && x.Content == candidate.Content,
                    cancellationToken),
                UnkeyedDuplicatePolicy.SourceIdentity when !string.IsNullOrWhiteSpace(candidate.SourceRef) => await db
                    .ContextItems.SingleOrDefaultAsync(x =>
                            x.OwnerId == candidate.OwnerId && x.WorkspaceId == candidate.WorkspaceId &&
                            x.Kind == candidate.Kind && x.Status == ContextStatus.Active &&
                            x.SourceType == candidate.SourceType && x.SourceRef == candidate.SourceRef,
                        cancellationToken),
                _ => null
            };
            if (duplicate is not null)
            {
                return new ContextPersistenceResult(duplicate, ContextPersistenceOutcome.ReusedExisting, duplicate.Id);
            }

            db.ContextItems.Add(candidate);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContextPersistenceResult(candidate, ContextPersistenceOutcome.Created);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ContextPersistenceResult(candidate, ContextPersistenceOutcome.ConcurrencyConflict);
        }
    }

    public async Task<ContextPersistenceResult> SupersedeByIdAsync(ContextItem candidate, Guid targetId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var target = await db.ContextItems.SingleOrDefaultAsync(x =>
                    x.OwnerId == candidate.OwnerId && x.Id == targetId && x.WorkspaceId == candidate.WorkspaceId &&
                    x.Status == ContextStatus.Active,
                cancellationToken);
            if (target is null)
            {
                return new ContextPersistenceResult(null, ContextPersistenceOutcome.InvalidTarget);
            }

            target.MarkSuperseded(now);
            candidate.SetSupersedes(target.Id);
            db.ContextItems.Add(candidate);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContextPersistenceResult(candidate, ContextPersistenceOutcome.Replaced, target.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ContextPersistenceResult(candidate, ContextPersistenceOutcome.ConcurrencyConflict);
        }
    }

    public async Task<ContextPersistenceResult> ArchiveAsync(Guid ownerId, Guid contextId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var context = await db.ContextItems.SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == contextId,
                cancellationToken);
            if (context is null)
            {
                return new ContextPersistenceResult(null, ContextPersistenceOutcome.NotFound);
            }

            if (context.Status == ContextStatus.Archived)
            {
                return new ContextPersistenceResult(context, ContextPersistenceOutcome.AlreadyArchived);
            }

            context.MarkArchived(now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContextPersistenceResult(context, ContextPersistenceOutcome.Replaced);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ContextPersistenceResult(null, ContextPersistenceOutcome.ConcurrencyConflict);
        }
    }

    private async Task LockWorkspaceAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT id FROM public.workspaces WHERE id = @workspace_id AND owner_id = @owner_id FOR UPDATE";
            AddParameter(command, "@workspace_id", workspaceId);
            AddParameter(command, "@owner_id", ownerId);
            await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, Guid value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
