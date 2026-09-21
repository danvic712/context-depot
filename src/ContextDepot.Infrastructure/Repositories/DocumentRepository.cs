using System.Data;
using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Documents.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class DocumentRepository(ContextDepotDbContext db) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid depotId, Guid documentId, CancellationToken cancellationToken) =>
        db.Documents.AsNoTracking().Include(x => x.Chunks).SingleOrDefaultAsync(x => x.DepotId == depotId && x.Id == documentId, cancellationToken);

    public Task<Document?> GetByPathAsync(Guid depotId, Guid workspaceId, string normalizedPath, CancellationToken cancellationToken) =>
        db.Documents.AsNoTracking().Include(x => x.Chunks).SingleOrDefaultAsync(x => x.DepotId == depotId && x.WorkspaceId == workspaceId && x.Path == normalizedPath, cancellationToken);

    public async Task MarkIndexPendingAsync(Guid depotId, Guid workspaceId, string normalizedPath, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var document = await db.Documents.SingleOrDefaultAsync(x => x.DepotId == depotId && x.WorkspaceId == workspaceId && x.Path == normalizedPath, cancellationToken);
        if (document?.Status == DocumentStatus.Active)
        {
            document.MarkPending(now);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<DocumentReconcilePersistenceResult> ReconcileIndexAsync(DocumentIndexWrite write, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var document = await db.Documents.Include(x => x.Chunks).SingleOrDefaultAsync(x =>
                x.DepotId == write.DepotId && x.WorkspaceId == write.WorkspaceId && (x.Id == write.DocumentId || x.Path == write.Path), cancellationToken);
            var isNew = document is null;
            var outcome = document is null
                ? DocumentReconcilePersistenceOutcome.Created
                : document.Status == DocumentStatus.Archived
                    ? DocumentReconcilePersistenceOutcome.Reactivated
                    : DocumentReconcilePersistenceOutcome.Reconciled;

            document ??= new Document(write.DocumentId, write.DepotId, write.WorkspaceId, write.Path, write.Title, write.Now);
            if (isNew)
            {
                db.Documents.Add(document);
            }

            var existingChunks = document.Chunks.ToArray();
            db.DocumentChunks.RemoveRange(existingChunks);
            document.Chunks.Clear();
            document.Reconcile(write.Title, write.ContentHash, write.Now);
            foreach (var chunk in write.Chunks.OrderBy(x => x.Ordinal))
            {
                document.Chunks.Add(new DocumentChunk(chunk.Id, write.DepotId, document.Id, write.WorkspaceId, chunk.Ordinal, chunk.HeadingPath, chunk.Content, chunk.ContentHash, write.Now));
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new DocumentReconcilePersistenceResult(document, outcome);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ContextDepotApplicationException(ApplicationErrorCodes.DocumentWriteFailed);
        }
    }

    public async Task<DocumentArchivePersistenceResult> ArchiveAsync(Guid depotId, Guid documentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var document = await db.Documents.SingleOrDefaultAsync(x => x.DepotId == depotId && x.Id == documentId, cancellationToken);
        if (document is null)
        {
            return new DocumentArchivePersistenceResult(DocumentArchivePersistenceOutcome.NotFound);
        }

        if (document.Status == DocumentStatus.Archived)
        {
            return new DocumentArchivePersistenceResult(DocumentArchivePersistenceOutcome.AlreadyArchived);
        }

        document.MarkArchived(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DocumentArchivePersistenceResult(DocumentArchivePersistenceOutcome.Archived);
    }
}
