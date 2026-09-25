using ContextDepot.Domain.Inferences.Enums;

namespace ContextDepot.Infrastructure.Configuration;

public sealed record EmbeddingRouteRuntimeSnapshot(
    string ProviderName,
    string ProtocolCode,
    Uri Endpoint,
    string ApiKey,
    string ModelName,
    int Dimensions,
    int TimeoutSeconds,
    string ProfileFingerprint)
{
    public override string ToString() => $"{nameof(EmbeddingRouteRuntimeSnapshot)} {{ ApiKey = [REDACTED] }}";
}

public sealed record InferenceRuntimeSnapshot(
    EmbeddingRouteRuntimeSnapshot? Embedding,
    InferenceRuntimeState State,
    string? DegradedReason);

public sealed class InferenceRuntimeSnapshotAccessor
{
    private InferenceRuntimeSnapshot? current;

    public InferenceRuntimeSnapshot Current =>
        Volatile.Read(ref current)
        ?? throw new InvalidOperationException("The inference runtime snapshot has not been loaded.");

    public void Publish(InferenceRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref current, snapshot);
    }
}
