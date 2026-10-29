namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed class ScopedInferenceRuntimeSnapshot(InferenceRuntimeSnapshot value)
{
    public InferenceRuntimeSnapshot Value { get; } = value;
}
