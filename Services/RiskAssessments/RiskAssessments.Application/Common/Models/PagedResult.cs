namespace RiskAssessments.Application.Common.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount);

/// <summary>
/// Cursor page. <see cref="NextCursor"/> is the last ObjectId on this page; pass it back as <c>afterId</c>.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
