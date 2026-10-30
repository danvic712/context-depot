namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed record InferenceRuntimeSnapshot(
    EmbeddingRouteRuntimeSnapshot? Embedding,
    InferenceRuntimeState State,
    string? DegradedReason,
    ChatRouteRuntimeSnapshot? Chat = null);
