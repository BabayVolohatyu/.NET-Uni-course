namespace Catalog.Bll.Queries;

/// <summary>
/// Generic paged response envelope.
/// totalCount reflects the total rows matching filters (not just on the current page).
/// Skip/Take are executed BEFORE ToListAsync — the DB returns only one page, not all rows.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
