using ContextDepot.Infrastructure.Exceptions;

namespace ContextDepot.Infrastructure.RuntimeConfiguration;

public sealed class InferenceRuntimeSnapshotAccessor
{
    private InferenceRuntimeSnapshot? current;

    public InferenceRuntimeSnapshot Current =>
        Volatile.Read(ref current)
        ?? throw new InvalidOperationException(InfrastructureErrorCodes.InferenceRuntimeSnapshotNotLoaded);

    public void Publish(InferenceRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref current, snapshot);
    }
}
