namespace Limita.Application.Common;

/// <summary>
/// Generic container for paginated query responses across the application.
/// </summary>
/// <typeparam name="T">The type of item contained in the paginated page.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The collection of data items for the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>
    /// The 1-based index of the current page.
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// The maximum number of items returned per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// The total count of matching items across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total number of available pages based on TotalCount and PageSize.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    /// <summary>
    /// Indicates whether a preceding page exists.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Indicates whether a subsequent page exists.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
