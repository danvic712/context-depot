namespace ContextDepot.Application.Overview;

/// <summary>
/// Contains ranked knowledge hits and retrieval availability information.
/// </summary>
/// <param name="Items">
/// The distinct hits in retrieval relevance order.
/// </param>
/// <param name="Degraded">
/// Whether retrieval reported a degraded search path.
/// </param>
/// <param name="Limit">
/// The effective retrieval limit, bounded by the configured maximum and 50.
/// </param>
public sealed record KnowledgeSearchResponse(IReadOnlyList<KnowledgeSearchHit> Items, bool Degraded, int Limit);
