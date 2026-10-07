namespace ContextDepot.Application.Tests.Setup;

internal sealed class SetupFixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
