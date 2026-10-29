namespace ContextDepot.Application.Shared.Runtime.Dtos;

/// <summary>The identity and display name of the authenticated current Depot.</summary>
/// <param name="DepotId">The current Depot identifier.</param>
/// <param name="DisplayName">The current Depot display name.</param>
public sealed record CurrentDepotModel(Guid DepotId, string DisplayName);
