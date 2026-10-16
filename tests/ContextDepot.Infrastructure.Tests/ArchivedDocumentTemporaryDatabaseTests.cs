using ContextDepot.Application.Documents.Contracts;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ContextDepot.Infrastructure.Tests;

// Uses only connection-local temporary tables; persistent application data is never changed.
public sealed class ArchivedDocumentTemporaryDatabaseTests
{
    [TemporaryPostgreSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_clears_chunks_all_profiles_and_source_and_rejects_late_vector_writes(bool archived)
    {
        await using var fixture = await TemporaryFixture.CreateAsync();
        var document = fixture.CreateDocument("old.md");
        var keep = fixture.CreateDocument("keep.md");
        fixture.Db.AddRange(document, keep); await fixture.Db.SaveChangesAsync();
        foreach (var table in fixture.Tables)
        {
            await fixture.UpsertVectorAsync(table, document);
            await fixture.UpsertVectorAsync(table, keep);
            Assert.Equal(2, await fixture.CountVectorsAsync(table));
        }
        if (archived) document.MarkArchived(fixture.Now);
        await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var repository = fixture.Repository();
        Assert.Equal(archived ? 1 : 0, (await repository.ListArchivedAsync(fixture.DepotId, fixture.SpaceId, new PaginationQuery(), default)).TotalCount);
        await repository.DeleteAsync(fixture.DepotId, document.Id, fixture.SpaceId, "notes/old.md", fixture.Now, default);
        Assert.False(await fixture.Db.Documents.AnyAsync(x => x.Id == document.Id));
        Assert.False(await fixture.Db.DocumentChunks.AnyAsync(x => x.DocumentId == document.Id));
        Assert.True(await fixture.Db.Documents.AnyAsync(x => x.Id == keep.Id));
        Assert.False(fixture.Markdown.Content.ContainsKey("notes/old.md"));
        Assert.True(fixture.Markdown.Content.ContainsKey("notes/keep.md"));
        foreach (var table in fixture.Tables)
        {
            Assert.Equal(1, await fixture.CountVectorsAsync(table));
            await fixture.UpsertVectorAsync(table, document);
            Assert.Equal(1, await fixture.CountVectorsAsync(table));
        }
    }

    [TemporaryPostgreSqlFact]
    public async Task File_failure_preserves_archived_metadata_chunks_vectors_and_content()
    {
        await using var fixture = await TemporaryFixture.CreateAsync();
        var document = fixture.CreateDocument("old.md");
        fixture.Db.Add(document); await fixture.Db.SaveChangesAsync();
        foreach (var table in fixture.Tables) await fixture.UpsertVectorAsync(table, document);
        document.MarkArchived(fixture.Now); await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        fixture.Markdown.FailDelete = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Repository().DeleteAsync(fixture.DepotId, document.Id,
            fixture.SpaceId, "notes/old.md", fixture.Now, default));
        Assert.True(await fixture.Db.Documents.AnyAsync(x => x.Id == document.Id));
        Assert.True(await fixture.Db.DocumentChunks.AnyAsync(x => x.DocumentId == document.Id));
        Assert.True(fixture.Markdown.Content.ContainsKey("notes/old.md"));
        foreach (var table in fixture.Tables) Assert.Equal(1, await fixture.CountVectorsAsync(table));
    }

    [TemporaryPostgreSqlFact]
    public async Task Commit_failure_restores_the_deleted_source_and_all_database_state()
    {
        await using var fixture = await TemporaryFixture.CreateAsync();
        var document = fixture.CreateDocument("old.md");
        fixture.Db.Add(document); await fixture.Db.SaveChangesAsync();
        foreach (var table in fixture.Tables) await fixture.UpsertVectorAsync(table, document);
        document.MarkArchived(fixture.Now); await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        await fixture.BlockCommitAsync(document.Id);
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Repository().DeleteAsync(fixture.DepotId, document.Id,
            fixture.SpaceId, "notes/old.md", fixture.Now, default));
        Assert.True(await fixture.Db.Documents.AnyAsync(x => x.Id == document.Id));
        Assert.True(await fixture.Db.DocumentChunks.AnyAsync(x => x.DocumentId == document.Id));
        Assert.Equal("canonical", fixture.Markdown.Content["notes/old.md"]);
        foreach (var table in fixture.Tables) Assert.Equal(1, await fixture.CountVectorsAsync(table));
    }

    [TemporaryPostgreSqlFact]
    public async Task Context_deletion_keeps_successors_clears_all_profiles_and_blocks_late_vectors()
    {
        await using var fixture = await TemporaryFixture.CreateAsync();
        var old = fixture.CreateContext("old"); var successor = fixture.CreateContext("successor");
        successor.SetSupersedes(old.Id);
        fixture.Db.AddRange(old, successor); await fixture.Db.SaveChangesAsync();
        foreach (var table in fixture.ContextTables)
        {
            await fixture.UpsertContextVectorAsync(table, old);
            await fixture.UpsertContextVectorAsync(table, successor);
        }
        fixture.Db.ChangeTracker.Clear();
        Assert.True(await fixture.ContextRepository().DeleteAsync(fixture.DepotId, old.Id, fixture.Now, default));
        Assert.False(await fixture.Db.ContextItems.AnyAsync(x => x.Id == old.Id));
        Assert.Null((await fixture.Db.ContextItems.SingleAsync(x => x.Id == successor.Id)).SupersedesId);
        foreach (var table in fixture.ContextTables)
        {
            Assert.Equal(1, await fixture.CountVectorsAsync(table));
            await fixture.UpsertContextVectorAsync(table, old);
            Assert.Equal(1, await fixture.CountVectorsAsync(table));
        }
    }

    [TemporaryPostgreSqlFact]
    public async Task Context_conflict_rolls_back_reference_changes_and_keeps_vectors()
    {
        await using var fixture = await TemporaryFixture.CreateAsync();
        var old = fixture.CreateContext("old"); var successor = fixture.CreateContext("successor");
        successor.SetSupersedes(old.Id);
        fixture.Db.AddRange(old, successor); await fixture.Db.SaveChangesAsync();
        foreach (var table in fixture.ContextTables) await fixture.UpsertContextVectorAsync(table, old);
        fixture.Db.ChangeTracker.Clear();
        Assert.False(await fixture.ContextRepository().DeleteAsync(fixture.DepotId, old.Id, fixture.Now.AddSeconds(-1), default));
        Assert.Equal(old.Id, (await fixture.Db.ContextItems.SingleAsync(x => x.Id == successor.Id)).SupersedesId);
        Assert.True(await fixture.Db.ContextItems.AnyAsync(x => x.Id == old.Id));
        foreach (var table in fixture.ContextTables) Assert.Equal(1, await fixture.CountVectorsAsync(table));
    }

    [TemporaryPostgreSqlFact]
    public async Task Context_deletion_cannot_target_a_different_depot()
    {
        await using var fixture = await TemporaryFixture.CreateAsync();
        var old = fixture.CreateContext("old"); fixture.Db.Add(old); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ContextDepot.Application.Shared.Exceptions.ContextDepotApplicationException>(() =>
            fixture.ContextRepository().DeleteAsync(Guid.CreateVersion7(), old.Id, fixture.Now, default));
        Assert.True(await fixture.Db.ContextItems.AnyAsync(x => x.Id == old.Id));
    }

    private sealed class TemporaryFixture : IAsyncDisposable
    {
        private NpgsqlDataSource source = null!;
        private NpgsqlConnection connection = null!;
        private string schema = "";
        public ContextDepotDbContext Db { get; private set; } = null!;
        public MemoryMarkdown Markdown { get; } = new();
        public Guid DepotId { get; } = Guid.CreateVersion7();
        public Guid SpaceId { get; } = Guid.CreateVersion7();
        public DateTimeOffset Now { get; } = new(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero);
        public string[] Tables { get; } = ["document_vectors_aaaaaaaaaaaaaaaa", "document_vectors_bbbbbbbbbbbbbbbb"];
        public string[] ContextTables { get; } = ["context_vectors_aaaaaaaaaaaaaaaa", "context_vectors_bbbbbbbbbbbbbbbb"];
        public static async Task<TemporaryFixture> CreateAsync()
        {
            var fixture = new TemporaryFixture();
            var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEMP_TEST_CONNECTION")) { Pooling = false };
            var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString); fixture.source = builder.Build();
            fixture.connection = await fixture.source.OpenConnectionAsync();
            try
            {
                await fixture.ExecuteAsync("""
                    CREATE TEMP TABLE documents (LIKE public.documents INCLUDING ALL);
                    CREATE TEMP TABLE context_items (LIKE public.context_items INCLUDING ALL);
                    ALTER TABLE context_items ADD FOREIGN KEY (supersedes_id, depot_id, workspace_id)
                    REFERENCES context_items (id, depot_id, workspace_id);
                    CREATE TEMP TABLE document_chunks (LIKE public.document_chunks INCLUDING ALL);
                    ALTER TABLE document_chunks ADD FOREIGN KEY (document_id, depot_id, workspace_id)
                    REFERENCES documents (id, depot_id, workspace_id) ON DELETE CASCADE;
                    """);
                await using (var command = new NpgsqlCommand("SELECT nspname FROM pg_namespace WHERE oid = pg_my_temp_schema()", fixture.connection))
                    fixture.schema = (string)(await command.ExecuteScalarAsync())!;
                foreach (var table in fixture.Tables)
                    await fixture.ExecuteAsync($"CREATE TEMP TABLE {PostgreSqlVectorSqlBuilder.QuoteIdentifier(table)} (document_chunk_id uuid PRIMARY KEY, document_id uuid NOT NULL, depot_id uuid NOT NULL, workspace_id uuid NOT NULL, embedding_input_hash text NOT NULL, embedding real[] NOT NULL)");
                foreach (var table in fixture.ContextTables)
                    await fixture.ExecuteAsync($"CREATE TEMP TABLE {PostgreSqlVectorSqlBuilder.QuoteIdentifier(table)} (context_item_id uuid PRIMARY KEY, depot_id uuid NOT NULL, workspace_id uuid NOT NULL, kind text NOT NULL, embedding_input_hash text NOT NULL, embedding real[] NOT NULL)");
                var access = new CurrentDepotAccessContext(); access.AllowInternalAccess();
                var options = new DbContextOptionsBuilder<ContextDepotDbContext>().UseNpgsql(fixture.connection)
                    .AddInterceptors(new InferenceTestSchemaInterceptor(fixture.schema)).Options;
                fixture.Db = new ContextDepotDbContext(options, access);
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public Document CreateDocument(string path)
        {
            var document = new Document(Guid.CreateVersion7(), DepotId, SpaceId, path, path, Now);
            document.Reconcile(path, "hash", Now);
            document.Chunks.Add(new DocumentChunk(Guid.CreateVersion7(), DepotId, document.Id, SpaceId, 0, "", "canonical", "hash", Now));
            Markdown.Content["notes/" + path] = "canonical";
            return document;
        }
        public DocumentManagementRepository Repository() => new(Db, Markdown,
            Microsoft.Extensions.Options.Options.Create(new PostgreSqlVectorStoreOptions { Schema = schema }));
        public ContextManagementRepository ContextRepository() => new(Db,
            Microsoft.Extensions.Options.Options.Create(new PostgreSqlVectorStoreOptions { Schema = schema }));
        public ContextItem CreateContext(string key) => new(Guid.CreateVersion7(), DepotId, SpaceId, ContextKind.Fact, key, key, "# Content", Now);
        public async Task UpsertContextVectorAsync(string table, ContextItem context)
        {
            var sql = PostgreSqlVectorSqlBuilder.BuildUpsert(schema, table, VectorCollectionDefinitions.CreateContext(3))
                .Replace("public.", PostgreSqlVectorSqlBuilder.QuoteIdentifier(schema) + ".", StringComparison.Ordinal);
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("record_ContextItemId", context.Id);
            command.Parameters.AddWithValue("record_DepotId", context.DepotId);
            command.Parameters.AddWithValue("record_WorkspaceId", context.WorkspaceId);
            command.Parameters.AddWithValue("record_Kind", "Fact");
            command.Parameters.AddWithValue("record_EmbeddingInputHash", "hash");
            command.Parameters.AddWithValue("record_Embedding", new float[] { 1, 0, 0 });
            await command.ExecuteNonQueryAsync();
        }
        public async Task UpsertVectorAsync(string table, Document document)
        {
            // Namespace substitution isolates the production SQL builder from persistent source tables.
            var sql = PostgreSqlVectorSqlBuilder.BuildUpsert(schema, table, VectorCollectionDefinitions.CreateDocument(3))
                .Replace("public.", PostgreSqlVectorSqlBuilder.QuoteIdentifier(schema) + ".", StringComparison.Ordinal);
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("record_DocumentChunkId", document.Chunks.Single().Id);
            command.Parameters.AddWithValue("record_DocumentId", document.Id);
            command.Parameters.AddWithValue("record_DepotId", DepotId);
            command.Parameters.AddWithValue("record_WorkspaceId", SpaceId);
            command.Parameters.AddWithValue("record_EmbeddingInputHash", "hash");
            command.Parameters.AddWithValue("record_Embedding", new float[] { 1, 0, 0 });
            await command.ExecuteNonQueryAsync();
        }
        public async Task BlockCommitAsync(Guid id)
        {
            await ExecuteAsync("CREATE TEMP TABLE deletion_guard (document_id uuid REFERENCES documents(id) DEFERRABLE INITIALLY DEFERRED)");
            await using var command = new NpgsqlCommand("INSERT INTO deletion_guard(document_id) VALUES (@id)", connection);
            command.Parameters.AddWithValue("id", id); await command.ExecuteNonQueryAsync();
        }
        public async Task<long> CountVectorsAsync(string table)
        {
            await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {PostgreSqlVectorSqlBuilder.QuoteQualifiedName(schema, table)}", connection);
            return (long)(await command.ExecuteScalarAsync())!;
        }
        private async Task ExecuteAsync(string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
        public async ValueTask DisposeAsync()
        {
            if (Db is not null) await Db.DisposeAsync();
            if (connection is not null) await connection.DisposeAsync();
            if (source is not null) await source.DisposeAsync();
        }
    }
    private sealed class MemoryMarkdown : IMarkdownStore
    {
        public Dictionary<string, string> Content { get; } = [];
        public bool FailDelete { get; set; }
        public Task<MarkdownDocument?> GetAsync(Guid depotId, string path, CancellationToken cancellationToken) =>
            Task.FromResult(Content.TryGetValue(path, out var content) ? new MarkdownDocument(path, content, "hash") : null);
        public Task DeleteAsync(Guid depotId, string path, CancellationToken cancellationToken)
        { if (FailDelete) throw new IOException("fixture failure"); Content.Remove(path); return Task.CompletedTask; }
        public Task WriteAtomicAsync(Guid depotId, string path, string content, CancellationToken cancellationToken)
        { Content[path] = content; return Task.CompletedTask; }
        public bool CanReadAndWrite() => true;
    }
}
