namespace ContextDepot.Application.Shared.Runtime.Contracts;

public interface ICurrentOwnerContext
{
    Guid OwnerId { get; }

    string DisplayName { get; }
}
