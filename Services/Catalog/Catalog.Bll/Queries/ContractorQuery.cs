namespace Catalog.Bll.Queries;

/// <summary>
/// Query parameters for GET /api/contractors — pagination, filtering, and sorting.
/// Bound from query string; all fields have sensible defaults.
/// </summary>
public sealed class ContractorQuery
{
    /// <summary>1-based page number (default: 1).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Items per page; clamped to [1, 100] in the service. Default: 20.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Free-text filter — matches company Name or EDRPOU (case-insensitive on DB).</summary>
    public string? Search { get; set; }

    /// <summary>Minimum registered authorized capital in UAH.</summary>
    public decimal? MinCapital { get; set; }

    /// <summary>Maximum registered authorized capital in UAH.</summary>
    public decimal? MaxCapital { get; set; }

    /// <summary>Filter by active/inactive status. Null = return all.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Sort field: "name" (default), "capital", "registrationDate".</summary>
    public string SortBy { get; set; } = "name";

    /// <summary>Sort direction: "asc" (default) or "desc".</summary>
    public string SortDir { get; set; } = "asc";
}
