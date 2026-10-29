namespace ContextDepot.Infrastructure.RuntimeConfiguration;

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
