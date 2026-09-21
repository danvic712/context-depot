namespace ContextDepot.Application.Shared.Runtime.Contracts;

public interface ICurrentDepotContext
{
    Guid DepotId { get; }

    string DisplayName { get; }
}
