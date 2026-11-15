using Ardalis.Specification;
using Catalog.Bll.Queries;
using Catalog.Domain.Models;

namespace Catalog.Bll.Specifications;

/// <summary>
/// Ardalis Specification that encapsulates the full filter/sort/page query for Contractor lists.
/// Separates query-building logic from both the service (which produces it) and the repository
/// (which applies it via WithSpecification).
///
/// Benefits over raw LINQ in the service:
///   - Testable independently of EF Core (pass mock query objects)
///   - Reusable: a second action (e.g. export, count) reuses the same spec
///   - Single Responsibility: service calls ListAsync(spec), not builds the query
///
/// Reference: Ardalis.Specification — https://specification.ardalis.com/
/// </summary>
public sealed class ContractorsFilterSpec : Specification<Contractor>
{
    public ContractorsFilterSpec(ContractorQuery q, int pageSize)
    {
        // ── Eager include of WorkCategories for the list view ──────────────────
        Query.Include(c => c.ContractorWorkCategories)
             .ThenInclude(cwc => cwc.WorkCategory);

        // ── Filtering ──────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(q.Search))
            Query.Where(c => c.Name.Contains(q.Search) || c.EDRPOU.Contains(q.Search));

        if (q.MinCapital is not null)
            Query.Where(c => c.AuthorizedCapital >= q.MinCapital);

        if (q.MaxCapital is not null)
            Query.Where(c => c.AuthorizedCapital <= q.MaxCapital);

        if (q.IsActive is not null)
            Query.Where(c => c.IsActive == q.IsActive);

        // ── Sorting — only allowed fields (prevents SQL injection via dynamic ORDER BY) ──
        switch ((q.SortBy.ToLowerInvariant(), q.SortDir.ToLowerInvariant()))
        {
            case ("capital", "desc"):
                Query.OrderByDescending(c => c.AuthorizedCapital);
                break;
            case ("capital", _):
                Query.OrderBy(c => c.AuthorizedCapital);
                break;
            case ("registrationdate", "desc"):
                Query.OrderByDescending(c => c.RegistrationDate);
                break;
            case ("registrationdate", _):
                Query.OrderBy(c => c.RegistrationDate);
                break;
            case (_, "desc"):
                Query.OrderByDescending(c => c.Name);
                break;
            default:
                Query.OrderBy(c => c.Name);
                break;
        }

        // ── Pagination — Skip/Take must be BEFORE ToListAsync ─────────────────
        Query.Skip((q.Page - 1) * pageSize).Take(pageSize);
    }
}

/// <summary>
/// Count-only spec (no pagination, no includes) used to get totalCount efficiently.
/// Same filters as ContractorsFilterSpec but without Skip/Take so COUNT(*) is fast.
/// </summary>
public sealed class ContractorsCountSpec : Specification<Contractor>
{
    public ContractorsCountSpec(ContractorQuery q)
    {
        if (!string.IsNullOrWhiteSpace(q.Search))
            Query.Where(c => c.Name.Contains(q.Search) || c.EDRPOU.Contains(q.Search));

        if (q.MinCapital is not null)
            Query.Where(c => c.AuthorizedCapital >= q.MinCapital);

        if (q.MaxCapital is not null)
            Query.Where(c => c.AuthorizedCapital <= q.MaxCapital);

        if (q.IsActive is not null)
            Query.Where(c => c.IsActive == q.IsActive);
    }
}
