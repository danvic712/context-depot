using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Infrastructure.Options;

namespace ContextDepot.Infrastructure.CurrentOwner;

public sealed class ConfiguredCurrentOwnerContext(CurrentOwnerOptions options) : ICurrentOwnerContext
{
    public Guid OwnerId { get; } = options.Id;

    public string DisplayName { get; } = options.DisplayName;
}
