namespace ContextDepot.Application.Documents.Dtos;

/// <summary>
/// Contains a consistent page of archived documents and the total count before pagination.
/// </summary>
/// <param name="Items">
/// The archived documents in descending update order, with identifiers breaking ties.
/// </param>
/// <param name="TotalCount">
/// The number of archived documents in the workspace before pagination.
/// </param>
/// <param name="Page">
/// The one-based page number.
/// </param>
/// <param name="PageSize">
/// The maximum number of documents requested per page.
/// </param>
public sealed record ArchivedDocumentPage(IReadOnlyList<ArchivedDocument> Items, int TotalCount, int Page, int PageSize);
