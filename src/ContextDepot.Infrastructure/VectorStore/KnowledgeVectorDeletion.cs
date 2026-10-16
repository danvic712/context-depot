using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ContextDepot.Infrastructure.VectorStore;

/// <summary>
/// Removes vectors for every stored embedding profile inside the source deletion transaction.
/// </summary>
internal static class KnowledgeVectorDeletion
{
    /// <summary>
    /// Removes a context's vectors from every stored embedding-profile table in the configured schema.
    /// </summary>
    /// <param name="db">
    /// The database context whose connection participates in the source deletion.
    /// </param>
    /// <param name="schema">
    /// The schema containing the vector-profile tables.
    /// </param>
    /// <param name="depotId">
    /// The owning depot identifier.
    /// </param>
    /// <param name="contextId">
    /// The context identifier whose vectors are removed.
    /// </param>
    /// <param name="transaction">
    /// The caller-owned source-deletion transaction.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that completes after all matching vector rows are removed within the supplied transaction.
    /// </returns>
    /// <remarks>
    /// The caller owns commit and rollback. Profiles are discovered from the database, including profiles no longer configured.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public static Task DeleteContextAsync(ContextDepotDbContext db, string schema, Guid depotId, Guid contextId,
        IDbContextTransaction transaction, CancellationToken cancellationToken) =>
        DeleteAsync(db, schema, depotId, contextId, "^context_vectors_[0-9a-fA-F]{16}$", "context_item_id", transaction, cancellationToken);

    /// <summary>
    /// Removes a document's vectors from every stored embedding-profile table in the configured schema.
    /// </summary>
    /// <param name="db">
    /// The database context whose connection participates in the source deletion.
    /// </param>
    /// <param name="schema">
    /// The schema containing the vector-profile tables.
    /// </param>
    /// <param name="depotId">
    /// The owning depot identifier.
    /// </param>
    /// <param name="documentId">
    /// The document identifier whose vectors are removed.
    /// </param>
    /// <param name="transaction">
    /// The caller-owned source-deletion transaction.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that completes after all matching vector rows are removed within the supplied transaction.
    /// </returns>
    /// <remarks>
    /// The caller owns commit and rollback. Profiles are discovered from the database, including profiles no longer configured.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// The operation was canceled before completion.
    /// </exception>
    public static Task DeleteDocumentAsync(ContextDepotDbContext db, string schema, Guid depotId, Guid documentId,
        IDbContextTransaction transaction, CancellationToken cancellationToken) =>
        DeleteAsync(db, schema, depotId, documentId, "^document_vectors_[0-9a-fA-F]{16}$", "document_id", transaction, cancellationToken);

    private static async Task DeleteAsync(ContextDepotDbContext db, string schema, Guid depotId, Guid id,
        string pattern, string column,
        IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var names = new List<string>();
        await using (var list = new NpgsqlCommand("""
            SELECT relation.relname FROM pg_catalog.pg_class AS relation
            JOIN pg_catalog.pg_namespace AS namespace ON namespace.oid = relation.relnamespace
            WHERE namespace.nspname = @schema AND relation.relkind IN ('r', 'p') AND relation.relname ~ @pattern;
            """, connection, (NpgsqlTransaction)transaction.GetDbTransaction()))
        {
            list.Parameters.AddWithValue("schema", schema);
            list.Parameters.AddWithValue("pattern", pattern);
            await using var reader = await list.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) names.Add(reader.GetString(0));
        }
        foreach (var name in names)
        {
            await using var delete = new NpgsqlCommand($"DELETE FROM {PostgreSqlVectorSqlBuilder.QuoteQualifiedName(schema, name)} WHERE depot_id = @depot AND {column} = @id;",
                connection, (NpgsqlTransaction)transaction.GetDbTransaction());
            delete.Parameters.AddWithValue("depot", depotId);
            delete.Parameters.AddWithValue("id", id);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
