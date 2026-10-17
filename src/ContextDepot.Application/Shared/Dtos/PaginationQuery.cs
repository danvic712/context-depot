namespace ContextDepot.Application.Shared.Dtos;

/// <summary>
/// Contains bounded, one-based pagination parameters.
/// </summary>
public record PaginationQuery
{
    /// <summary>
    /// The default number of items returned per page.
    /// </summary>
    public const int DefaultPageSize = 12;

    /// <summary>
    /// The largest supported page number.
    /// </summary>
    public const int MaximumPage = 100000;

    /// <summary>
    /// The largest supported page size.
    /// </summary>
    public const int MaximumPageSize = 60;

    /// <summary>
    /// Gets the one-based page number. Defaults to the first page.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Gets the number of items per page, from 1 to 60.
    /// </summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// Indicates whether both pagination parameters are supported.
    /// </summary>
    public bool IsValid() => Page is >= 1 and <= MaximumPage && PageSize is >= 1 and <= MaximumPageSize;
}
