namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed record ChatRouteRuntimeSnapshot(Uri Endpoint, string ApiKey, string ModelName, int TimeoutSeconds)
{
    public override string ToString() => $"{nameof(ChatRouteRuntimeSnapshot)} {{ ApiKey = [REDACTED] }}";
}
