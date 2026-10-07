using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Tests.Setup;

internal sealed class SetupTestIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
