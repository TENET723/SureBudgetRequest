namespace SureBudgetRequest.Application.Common;

/// <summary>
/// A single page of results plus the total row count of the underlying query
/// (before paging). <see cref="TotalPages"/>, <see cref="HasPrev"/> and
/// <see cref="HasNext"/> are derived for paging-UI convenience.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
}
