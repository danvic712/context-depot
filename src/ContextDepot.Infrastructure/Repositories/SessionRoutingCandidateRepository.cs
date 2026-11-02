using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Infrastructure.RuntimeConfiguration;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class SessionRoutingCandidateRepository(ScopedInferenceRuntimeSnapshot runtime,
    EmbeddingGeneratorService embeddings, ISemanticRetrievalRepository semantic,
    ISessionCaptureInboxRepository inbox, ISessionCaptureCallBudget budget,
    ISessionContentProtector protector, TimeProvider time) : ISessionRoutingCandidateRepository
{
    public async Task<RoutingCandidateSet> GetCandidatesAsync(RouteLease lease, AuthorizedWorkspaceSnapshot scope,
        CancellationToken cancellationToken)
    {
        if (scope.DepotId != lease.Locator.DepotId || scope.KeyId != lease.Locator.KeyId)
            return new([], Outcome: new("permanent_failure", ResultCode: "unauthorized"));
        var profile = runtime.Value.Embedding;
        if (scope.Workspaces.Count is 0 or > 512 || profile is null)
            return RoutingCandidatePolicy.Select(scope, lease.UserContent, new Dictionary<Guid, double>(), profile is not null);
        float[]? vector = null;
        if (lease.EncryptedRoutingVector is not null && lease.RoutingEmbeddingFingerprint == profile.ProfileFingerprint)
        {
            try
            {
                var json = protector.Unprotect(lease.EncryptedRoutingVector);
                if (Encoding.UTF8.GetByteCount(json) <= 256 * 1024 && Hash(json) == lease.RoutingVectorHash)
                {
                    var stored = JsonSerializer.Deserialize<StoredVector>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (stored?.UserContentHash == lease.UserContentHash && stored.ProfileFingerprint == profile.ProfileFingerprint)
                        vector = stored.Vector;
                }
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { }
        }
        if (vector is not null && (vector.Length != profile.Dimensions || vector.Any(v => !float.IsFinite(v)))) vector = null;
        if (vector is null)
        {
            var reservation = await budget.TryReserveAsync(new(scope.DepotId, scope.KeyId, "route", lease.Locator.RouteId,
                lease.LeaseToken), ProviderCallKind.RoutingEmbedding, cancellationToken);
            if (reservation.Status != "granted") return new([], Outcome: ReservationDecision(reservation));
            var generated = await embeddings.GenerateAsync([lease.UserContent], cancellationToken);
            vector = generated.Single().ToArray();
            if (vector.Length != profile.Dimensions || vector.Any(v => !float.IsFinite(v)))
                return new([], Outcome: new("transient_failure", ResultCode: "embedding_unavailable"));
            var json = JsonSerializer.Serialize(new StoredVector(lease.UserContentHash, profile.ProfileFingerprint, vector),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (Encoding.UTF8.GetByteCount(json) > 256 * 1024)
                return new([], Outcome: new("permanent_failure", ResultCode: "content_unavailable"));
            if (!await inbox.StoreRoutingVectorAsync(lease, protector.Protect(json), Hash(json), profile.ProfileFingerprint, cancellationToken))
                return new([], Outcome: new("transient_failure", ResultCode: "lease_lost"));
        }
        var ids = scope.Workspaces.Select(w => w.Id).Distinct().ToArray();
        var query = new SemanticCandidateQuery(scope.DepotId, ids, null, 129, time.GetUtcNow());
        var contexts = await semantic.FindContextCandidatesAsync(query, vector, cancellationToken);
        var documents = await semantic.FindDocumentCandidatesAsync(query, vector, cancellationToken);
        var scores = contexts.Where(c => c.DepotId == scope.DepotId && ids.Contains(c.WorkspaceId))
            .Select(c => (c.WorkspaceId, c.Similarity)).Concat(documents
                .Where(c => c.DepotId == scope.DepotId && ids.Contains(c.WorkspaceId)).Select(c => (c.WorkspaceId, c.Similarity))).ToArray();
        var truncated = contexts.Count >= 129 && contexts[128].Similarity >= .82 ||
                        documents.Count >= 129 && documents[128].Similarity >= .82;
        return RoutingCandidatePolicy.Select(scope, lease.UserContent,
            scores.GroupBy(s => s.WorkspaceId).ToDictionary(g => g.Key, g => g.Max(s => s.Similarity)), true, truncated);
    }

    public static RouteDecision ReservationDecision(BudgetReservation reservation) => reservation.Status == "wait"
        ? new("budget_deferred", ResultCode: "budget_deferred", NotBefore: reservation.WaitUntil)
        : new("transient_failure", ResultCode: reservation.Status == "expired" ? "expired" : "lease_lost");

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private sealed record StoredVector(string UserContentHash, string ProfileFingerprint, float[] Vector);
}
