using ContextDepot.Application.Abstractions;

namespace ContextDepot.Infrastructure.CurrentOwner;

public sealed class ConfiguredCurrentOwnerContext(CurrentOwnerOptions options) : ICurrentOwnerContext
{
    public Guid OwnerId { get; } = options.Id;

    public string DisplayName { get; } = options.DisplayName;
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class GuidV7IdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
