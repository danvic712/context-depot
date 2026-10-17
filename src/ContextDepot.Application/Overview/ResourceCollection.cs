namespace ContextDepot.Application.Overview;

/// <summary>
/// Contains an overview collection with its observation time and continuation indicator.
/// </summary>
/// <typeparam name="T">
/// The type of each item in the collection.
/// </typeparam>
/// <param name="AsOf">
/// The observation time used to evaluate time-sensitive context visibility.
/// </param>
/// <param name="Items">
/// The requested items in the collection order.
/// </param>
/// <param name="HasMore">
/// Whether additional matching items exist beyond the requested limit.
/// </param>
public sealed record ResourceCollection<T>(DateTimeOffset AsOf, IReadOnlyList<T> Items, bool HasMore);
