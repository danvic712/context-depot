using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Infrastructure.Shared;

public sealed class GuidV7IdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}