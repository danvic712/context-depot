using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Documents;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using ContextDepot.Application.Embeddings;
using ContextDepot.Infrastructure.HealthChecks;
using ContextDepot.Infrastructure.Options;

namespace ContextDepot.Infrastructure.Tests;

public sealed class BackendReviewRegressionTests
{
    [PostgreSqlFact]
    public async Task Lexical_queries_find_old_low_importance_text_json_and_chinese_before_limiting()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var now = fixture.Now;
        for (var index = 0; index < 1_010; index++)
        {
            var context = fixture.Context("ordinary rarelexicalneedles rarejsontags raremetadataextra", now);
            context.SetQuality(100, null);
            db.Add(context);
        }
        var text = fixture.Context("rarelexicalneedle", now.AddDays(-1));
        var json = fixture.Context("old content", now.AddDays(-1));
        json.SetTags("[\"rarejsontag\"]"); json.SetMetadata("{\"topic\":\"raremetadata\"}");
        var chinese = fixture.Context("知识检索性能", now.AddDays(-1));
        var keyed = new ContextItem(Guid.CreateVersion7(), fixture.Depot.Id, fixture.Root.Id,
            ContextKind.Decision, "project.database", null, "old decision", now.AddDays(-1));
        db.AddRange(text, json, chinese, keyed);
        var filler = fixture.Document("new.md", now);
        for (var index = 0; index < 1_010; index++)
            filler.Chunks.Add(fixture.Chunk(filler, "unrelated chunk", now, index));
        var old = fixture.Document("old.md", now.AddDays(-1));
        var oldChunk = fixture.Chunk(old, "raredocumentneedle 中文分块", now.AddDays(-1));
        old.Chunks.Add(oldChunk);
        db.AddRange(filler, old); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new ContextQueryRepository(db, new VisibleWorkspaceTopologyProvider(db));
        foreach (var (query, expected) in new[] { ("rarelexicalneedle", text.Id), ("RAREJSONTAG", json.Id),
                     ("raremetadata", json.Id), ("知识检索", chinese.Id), ("project.database", keyed.Id) })
        {
            var result = await repository.FindLexicalContextCandidatesAsync(
                new ContextSearchQuery(fixture.Depot.Id, null, null, query, now, 2), default);
            Assert.Contains(result, item => item.ContextItemId == expected);
        }
        foreach (var query in new[] { "raredocumentneedle", "中文分块" })
        {
            var result = await repository.FindLexicalDocumentCandidatesAsync(
                new ContextSearchQuery(fixture.Depot.Id, null, null, query, now, 2), default);
            Assert.Equal(oldChunk.Id, Assert.Single(result).DocumentChunkId);
        }
        var bootstrap = new BootstrapRepository(db, new VisibleWorkspaceTopologyProvider(db));
        Assert.Equal(text.Id, Assert.Single(await bootstrap.FindContextCandidatesAsync(
            new BootstrapQuery(fixture.Depot.Id, new HashSet<Guid> { fixture.Root.Id }, now, 2, "rarelexicalneedle"), default)).Id);
    }

    [PostgreSqlFact]
    public async Task Semantic_limit_excludes_orphans_archived_expired_and_inaccessible_sources_and_cleanup_spans_profiles()
    {
        await using var fixture = await Fixture.CreateAsync();
        var active = fixture.Context("valid current knowledge", fixture.Now);
        var expired = fixture.Context("expired", fixture.Now); expired.SetValidity(null, null, fixture.Now);
        var hidden = fixture.Context("not granted", fixture.Now, fixture.Other.Id);
        var document = fixture.Document("valid.md", fixture.Now);
        var chunk = fixture.Chunk(document, "valid document", fixture.Now); document.Chunks.Add(chunk);
        fixture.Db.AddRange(active, expired, hidden, document);
        var archived = Enumerable.Range(0, 64).Select(_ => fixture.Context("old knowledge", fixture.Now)).ToArray();
        foreach (var context in archived) context.MarkArchived(fixture.Now);
        fixture.Db.AddRange(archived); await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var first = new string('a', 64); var second = new string('b', 64);
        foreach (var profile in new[] { first, second })
        {
            var contexts = fixture.Store.GetCollection<Guid, ContextVectorRecord>(
                VectorCollectionNamePolicy.CreateContextCollectionName(profile), VectorCollectionDefinitions.CreateContext(3));
            var documents = fixture.Store.GetCollection<Guid, DocumentVectorRecord>(
                VectorCollectionNamePolicy.CreateDocumentCollectionName(profile), VectorCollectionDefinitions.CreateDocument(3));
            await contexts.EnsureCollectionExistsAsync(); await documents.EnsureCollectionExistsAsync();
            await contexts.UpsertAsync(archived.Concat([expired, hidden]).Select(context => new ContextVectorRecord
            {
                ContextItemId = context.Id, DepotId = fixture.Depot.Id, WorkspaceId = context.WorkspaceId,
                Kind = "fact", EmbeddingInputHash = "hash", Embedding = new float[] { 1, 0, 0 }
            }).Append(new ContextVectorRecord { ContextItemId = active.Id, DepotId = fixture.Depot.Id,
                WorkspaceId = fixture.Root.Id, Kind = "fact", EmbeddingInputHash = "hash", Embedding = new float[] { .8f, .2f, 0 } }));
            await documents.UpsertAsync(Enumerable.Range(0, 64).Select(_ => new DocumentVectorRecord
            {
                DocumentChunkId = Guid.CreateVersion7(), DocumentId = document.Id, DepotId = fixture.Depot.Id,
                WorkspaceId = fixture.Root.Id, EmbeddingInputHash = "hash", Embedding = new float[] { 1, 0, 0 }
            }).Append(new DocumentVectorRecord { DocumentChunkId = chunk.Id, DocumentId = document.Id,
                DepotId = fixture.Depot.Id, WorkspaceId = fixture.Root.Id, EmbeddingInputHash = "hash",
                Embedding = new float[] { .8f, .2f, 0 } }));
        }
        var access = new CurrentDepotAccessContext();
        access.Initialize(new DepotAccessKeyIdentity(Guid.CreateVersion7(), fixture.Depot.Id, "test", [fixture.Root.Id], [fixture.Root.Id]));
        await using var restrictedDb = new ContextDepotDbContext(fixture.Options, access);
        var snapshot = new ScopedInferenceRuntimeSnapshot(new InferenceRuntimeSnapshot(
            new EmbeddingRouteRuntimeSnapshot("test", "openai-compatible", new Uri("https://example.test"), "unused", "model", 3, 30, first),
            InferenceRuntimeState.Ready, null));
        var log = new FailureLogger<VectorDataSemanticRetrievalRepository>();
        var repository = new VectorDataSemanticRetrievalRepository(fixture.Store, restrictedDb, snapshot, log);
        var query = new SemanticCandidateQuery(fixture.Depot.Id, null, [ContextKind.Fact], 1, fixture.Now, 1);
        try
        {
            Assert.Equal(active.Id, Assert.Single(await repository.FindContextCandidatesAsync(query, new float[] { 1, 0, 0 }, default)).Context.Id);
            Assert.Equal(chunk.Id, Assert.Single(await repository.FindDocumentCandidatesAsync(query, new float[] { 1, 0, 0 }, default)).Document.Id);
        }
        catch (ContextDepotApplicationException)
        {
            throw new InvalidOperationException("Semantic search failed", log.Failure);
        }
        for (var cycle = 0; cycle < 3; cycle++) await fixture.Store.RemoveObsoleteVectorsAsync(fixture.Depot.Id, 32, fixture.Now, default);
        foreach (var profile in new[] { first, second })
        {
            var contexts = fixture.Store.GetCollection<Guid, ContextVectorRecord>(
                VectorCollectionNamePolicy.CreateContextCollectionName(profile), VectorCollectionDefinitions.CreateContext(3));
            Assert.Null(await contexts.GetAsync(archived[0].Id)); Assert.Null(await contexts.GetAsync(expired.Id));
            Assert.NotNull(await contexts.GetAsync(active.Id));
            // Valid but inaccessible knowledge must be preserved by internal cleanup.
            Assert.NotNull(await contexts.GetAsync(hidden.Id));
            var documents = fixture.Store.GetCollection<Guid, DocumentVectorRecord>(
                VectorCollectionNamePolicy.CreateDocumentCollectionName(profile), VectorCollectionDefinitions.CreateDocument(3));
            var retained = new List<DocumentVectorRecord>();
            await foreach (var record in documents.GetAsync(record => record.DepotId == fixture.Depot.Id, 100)) retained.Add(record);
            Assert.Equal(chunk.Id, Assert.Single(retained).DocumentChunkId);
        }
    }

    [PostgreSqlFact]
    public async Task Restricted_key_creates_multiple_missing_descendants_but_cannot_create_an_ungranted_root()
    {
        await using var fixture = await Fixture.CreateAsync();
        var key = new DepotAccessKey(Guid.CreateVersion7(), fixture.Depot.Id, "test", "test-prefix", "hash", fixture.Now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, fixture.Depot.Id, fixture.Root.Id, fixture.Now));
        fixture.Db.Add(key); await fixture.Db.SaveChangesAsync();
        var access = new CurrentDepotAccessContext();
        access.Initialize(new DepotAccessKeyIdentity(key.Id, fixture.Depot.Id, "test", [fixture.Root.Id], [fixture.Root.Id]));
        await using var db = new ContextDepotDbContext(fixture.Options, access);
        var repository = new WorkspaceRepository(db, new GuidV7IdGenerator(), access, new VisibleWorkspaceTopologyProvider(db));
        var result = await repository.UpsertPathAsync(fixture.Depot.Id, "root/new/deep", "Deep", null, "{}", true, fixture.Now, default);
        Assert.Equal(WorkspaceUpsertPersistenceOutcome.Created, result.Outcome);
        Assert.True(access.CanAccess(result.Workspace!.Id));
        var children = await fixture.Db.Workspaces.Where(workspace => workspace.DepotId == fixture.Depot.Id && workspace.ParentWorkspaceId != null).ToArrayAsync();
        Assert.Equal(2, children.Length);
        Assert.Equal(3, await fixture.Db.WorkspaceAccessGrants.CountAsync(grant => grant.DepotAccessKeyId == key.Id));
        var rejected = await repository.UpsertPathAsync(fixture.Depot.Id, "forbidden/child", "Child", null, "{}", true, fixture.Now, default);
        Assert.Equal(WorkspaceUpsertPersistenceOutcome.ParentNotFound, rejected.Outcome);
        Assert.False(await fixture.Db.Workspaces.AnyAsync(workspace => workspace.DepotId == fixture.Depot.Id && workspace.Slug == "forbidden"));
    }

    [PostgreSqlFact]
    public async Task Repair_candidates_rotate_after_failure_and_reconcile_preserves_unchanged_chunk_ids()
    {
        await using var fixture = await Fixture.CreateAsync();
        var bad = new Document(Guid.CreateVersion7(), fixture.Depot.Id, fixture.Root.Id, "bad.md", "Bad", fixture.Now.AddDays(-2));
        var good = new Document(Guid.CreateVersion7(), fixture.Depot.Id, fixture.Root.Id, "good.md", "Good", fixture.Now.AddDays(-1));
        fixture.Db.AddRange(bad, good); await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var repair = new PostgreSqlIndexRepairRepository(fixture.Db, TimeProvider.System);
        Assert.Equal(bad.Id, Assert.Single(await repair.FindDocumentIndexRepairCandidatesAsync(fixture.Depot.Id, 1, default)).DocumentId);
        await repair.MarkDocumentIndexFailedAsync(fixture.Depot.Id, bad.Id, ApplicationErrorCodes.MarkdownFileMissing, default);
        Assert.Equal(good.Id, Assert.Single(await repair.FindDocumentIndexRepairCandidatesAsync(fixture.Depot.Id, 1, default)).DocumentId);
        var repository = new DocumentRepository(fixture.Db);
        var id = Guid.CreateVersion7();
        var write = new DocumentIndexWrite(good.Id, fixture.Depot.Id, fixture.Root.Id, good.Path, good.Title,
            "hash", [new DocumentChunkWrite(id, 0, "Heading", "content", "chunk-hash")], fixture.Now);
        await repository.ReconcileIndexAsync(write, default);
        var again = await repository.ReconcileIndexAsync(write with
        { Chunks = [new DocumentChunkWrite(Guid.CreateVersion7(), 0, "Heading", "content", "chunk-hash")] }, default);
        Assert.Equal(id, Assert.Single(again.Document.Chunks).Id);
    }

    [PostgreSqlFact]
    public async Task Failed_reconcile_discards_pending_changes_so_failure_can_be_saved_and_next_document_succeeds()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.Document("first.md", fixture.Now);
        var firstChunk = fixture.Chunk(first, "original", fixture.Now); first.Chunks.Add(firstChunk);
        var other = fixture.Document("other.md", fixture.Now);
        var collision = fixture.Chunk(other, "other content", fixture.Now); other.Chunks.Add(collision);
        fixture.Db.AddRange(first, other); await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var repository = new DocumentRepository(fixture.Db);
        var write = new DocumentIndexWrite(first.Id, fixture.Depot.Id, fixture.Root.Id, first.Path, first.Title,
            "changed-hash", [new DocumentChunkWrite(collision.Id, 0, "", "changed", "changed-hash")], fixture.Now);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.ReconcileIndexAsync(write, default));
        Assert.Equal(ApplicationErrorCodes.DocumentWriteFailed, error.ErrorCode);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
        await new PostgreSqlIndexRepairRepository(fixture.Db, TimeProvider.System)
            .MarkDocumentIndexFailedAsync(fixture.Depot.Id, first.Id, error.ErrorCode, default);
        Assert.Equal(DocumentIndexStatus.Failed, (await repository.GetByIdAsync(fixture.Depot.Id, first.Id, default))!.IndexStatus);
        Assert.Equal("original", (await fixture.Db.DocumentChunks.AsNoTracking().SingleAsync(chunk => chunk.Id == firstChunk.Id)).Content);
        var valid = await repository.ReconcileIndexAsync(write with
        { Chunks = [new DocumentChunkWrite(Guid.CreateVersion7(), 0, "", "changed", "changed-hash")] }, default);
        Assert.Equal(DocumentIndexStatus.Indexed, valid.Document.IndexStatus);
    }

    [PostgreSqlFact]
    public async Task Vector_batch_rolls_back_all_records_on_a_database_error()
    {
        await using var fixture = await Fixture.CreateAsync();
        var collection = fixture.Store.GetCollection<Guid, ContextVectorRecord>(
            VectorCollectionNamePolicy.CreateContextCollectionName(new string('c', 64)), VectorCollectionDefinitions.CreateContext(3));
        await collection.EnsureCollectionExistsAsync();
        var records = Enumerable.Range(0, 32).Select(_ => new ContextVectorRecord
        { ContextItemId = Guid.CreateVersion7(), DepotId = fixture.Depot.Id, WorkspaceId = fixture.Root.Id,
            Kind = "fact", EmbeddingInputHash = "hash", Embedding = new float[] { 1, 0, 0 } }).ToArray();
        var invalid = new ContextVectorRecord { ContextItemId = Guid.CreateVersion7(), DepotId = fixture.Depot.Id,
            WorkspaceId = fixture.Root.Id, Kind = "fact", EmbeddingInputHash = "hash", Embedding = new float[] { float.NaN, 0, 0 } };
        await Assert.ThrowsAsync<PostgresException>(() => collection.UpsertAsync(records.Append(invalid)));
        Assert.Null(await collection.GetAsync(records[0].ContextItemId));
        await collection.UpsertAsync(records);
        Assert.NotNull(await collection.GetAsync(records[^1].ContextItemId));
        var ids = records.Take(2).Select(record => record.ContextItemId).ToArray();
        var kinds = new[] { "fact", "goal" };
        var matching = new List<ContextVectorRecord>();
        await foreach (var record in collection.GetAsync(record => ids.Contains(record.ContextItemId) && kinds.Contains(record.Kind), 10))
            matching.Add(record);
        Assert.Equal(ids.Order(), matching.Select(record => record.ContextItemId).Order());
    }

    [PostgreSqlFact]
    public async Task Concurrent_coverage_probes_share_one_paged_computation()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.AddRange(Enumerable.Range(0, 300).Select(_ => fixture.Context("coverage source", fixture.Now)));
        await fixture.Db.SaveChangesAsync();
        var profile = new string('d', 64);
        await fixture.Store.GetCollection<Guid, ContextVectorRecord>(VectorCollectionNamePolicy.CreateContextCollectionName(profile),
            VectorCollectionDefinitions.CreateContext(3)).EnsureCollectionExistsAsync();
        await fixture.Store.GetCollection<Guid, DocumentVectorRecord>(VectorCollectionNamePolicy.CreateDocumentCollectionName(profile),
            VectorCollectionDefinitions.CreateDocument(3)).EnsureCollectionExistsAsync();
        var snapshot = new ScopedInferenceRuntimeSnapshot(new InferenceRuntimeSnapshot(new EmbeddingRouteRuntimeSnapshot(
            "test", "openai-compatible", new Uri("https://example.test"), "unused", "model", 3, 30, profile), InferenceRuntimeState.Ready, null));
        var counter = new CoverageQueryCounter();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>(fixture.Options).AddInterceptors(counter).Options;
        var access = new CurrentDepotAccessContext(); access.AllowInternalAccess();
        await using var firstDb = new ContextDepotDbContext(options, access);
        await using var secondDb = new ContextDepotDbContext(options, access);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var gate = new VectorCoverageComputationGate();
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var monitor = services.GetRequiredService<IOptionsMonitor<VectorCoverageOptions>>();
        VectorCoverageSnapshotProvider Create(ContextDepotDbContext db) => new(db,
            new VectorDataVectorIndexRepository(fixture.Store, snapshot), new ContextEmbeddingTextBuilder(),
            new DocumentEmbeddingTextBuilder(), cache, snapshot, monitor, gate);
        var first = Create(firstDb); var second = Create(secondDb);
        var results = await Task.WhenAll(first.GetAsync(fixture.Depot.Id, fixture.Now, default), second.GetAsync(fixture.Depot.Id, fixture.Now, default));
        Assert.All(results, result => { Assert.Equal(300, result.ContextTotal); Assert.Equal(0, result.ContextIndexed); });
        Assert.Equal(2, counter.ContextReads);
        Assert.Equal(results[0], await second.GetAsync(fixture.Depot.Id, fixture.Now, default));
        Assert.Equal(2, counter.ContextReads);
    }

    [PostgreSqlFact]
    public async Task Last_used_updates_are_throttled_while_revocation_remains_immediate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var hasher = new DepotAccessKeySecretHasher(); var secret = hasher.Generate();
        var key = new DepotAccessKey(Guid.CreateVersion7(), fixture.Depot.Id, "test", secret.Prefix, secret.SecretHash, fixture.Now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, fixture.Depot.Id, fixture.Root.Id, fixture.Now));
        fixture.Db.Add(key); await fixture.Db.SaveChangesAsync(); fixture.Db.ChangeTracker.Clear();
        var clock = new ManualClock(fixture.Now);
        var authenticator = new DepotAccessKeyAuthenticator(fixture.Db, hasher, clock);
        Assert.NotNull(await authenticator.AuthenticateAsync(secret.Plaintext));
        clock.Now = fixture.Now.AddSeconds(30);
        Assert.NotNull(await authenticator.AuthenticateAsync(secret.Plaintext));
        Assert.Equal(fixture.Now, await fixture.Db.DepotAccessKeys.Where(item => item.Id == key.Id).Select(item => item.LastUsedAt).SingleAsync());
        clock.Now = fixture.Now.AddMinutes(2);
        Assert.NotNull(await authenticator.AuthenticateAsync(secret.Plaintext));
        Assert.Equal(clock.Now, await fixture.Db.DepotAccessKeys.Where(item => item.Id == key.Id).Select(item => item.LastUsedAt).SingleAsync());
        await fixture.Db.DepotAccessKeys.Where(item => item.Id == key.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, clock.Now));
        Assert.Null(await authenticator.AuthenticateAsync(secret.Plaintext));
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CoverageQueryCounter : DbCommandInterceptor
    {
        public int ContextReads;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM public.context_items", StringComparison.Ordinal)) Interlocked.Increment(ref ContextReads);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailureLogger<T> : ILogger<T>
    {
        public Exception? Failure { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Failure = exception;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public DateTimeOffset Now { get; } = new(DateTimeOffset.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero);
        public Depot Depot { get; private set; } = null!;
        public Workspace Root { get; private set; } = null!;
        public Workspace Other { get; private set; } = null!;
        public ContextDepotDbContext Db { get; private set; } = null!;
        public DbContextOptions<ContextDepotDbContext> Options { get; private set; } = null!;
        public PostgreSqlVectorStore Store { get; private set; } = null!;
        private NpgsqlDataSource dataSource = null!;
        private readonly string schema = "review_vectors_" + Guid.CreateVersion7().ToString("N");

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            var connection = Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")!;
            fixture.Options = new DbContextOptionsBuilder<ContextDepotDbContext>().UseNpgsql(connection).Options;
            var access = new CurrentDepotAccessContext(); access.AllowInternalAccess();
            fixture.Db = new ContextDepotDbContext(fixture.Options, access);
            fixture.Depot = new Depot(Guid.CreateVersion7(), "Review regression", fixture.Now);
            fixture.Root = new Workspace(Guid.CreateVersion7(), fixture.Depot.Id, null, "Root", "root", null, fixture.Now);
            fixture.Other = new Workspace(Guid.CreateVersion7(), fixture.Depot.Id, null, "Other", "other", null, fixture.Now);
            fixture.Db.AddRange(fixture.Depot, fixture.Root, fixture.Other); await fixture.Db.SaveChangesAsync();
            var builder = new NpgsqlDataSourceBuilder(connection); builder.UseVector(); fixture.dataSource = builder.Build();
            fixture.Store = new PostgreSqlVectorStore(fixture.dataSource, Microsoft.Extensions.Options.Options.Create(new PostgreSqlVectorStoreOptions { Schema = fixture.schema }));
            return fixture;
        }

        public ContextItem Context(string content, DateTimeOffset now, Guid? workspaceId = null) =>
            new(Guid.CreateVersion7(), Depot.Id, workspaceId ?? Root.Id, ContextKind.Fact, null, null, content, now);
        public Document Document(string path, DateTimeOffset now)
        {
            var document = new Document(Guid.CreateVersion7(), Depot.Id, Root.Id, path, path, now);
            document.Reconcile(path, "hash", now); return document;
        }
        public DocumentChunk Chunk(Document document, string content, DateTimeOffset now, int ordinal = 0) =>
            new(Guid.CreateVersion7(), Depot.Id, document.Id, Root.Id, ordinal, "", content, DocumentContentHasher.Compute(content), now);

        public async ValueTask DisposeAsync()
        {
            Db.ChangeTracker.Clear();
            await Db.WorkspaceAccessGrants.Where(item => item.DepotId == Depot.Id).ExecuteDeleteAsync();
            await Db.DepotAccessKeys.Where(item => item.DepotId == Depot.Id).ExecuteDeleteAsync();
            await Db.DocumentChunks.Where(item => item.DepotId == Depot.Id).ExecuteDeleteAsync();
            await Db.Documents.Where(item => item.DepotId == Depot.Id).ExecuteDeleteAsync();
            await Db.ContextItems.Where(item => item.DepotId == Depot.Id).ExecuteDeleteAsync();
            while (await Db.Workspaces.AnyAsync(item => item.DepotId == Depot.Id))
                await Db.Workspaces.Where(item => item.DepotId == Depot.Id && !Db.Workspaces.Any(child => child.ParentWorkspaceId == item.Id)).ExecuteDeleteAsync();
            await Db.Depots.Where(item => item.Id == Depot.Id).ExecuteDeleteAsync();
            await using var command = dataSource.CreateCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;");
            await command.ExecuteNonQueryAsync();
            await Db.DisposeAsync(); await dataSource.DisposeAsync();
        }
    }
}
