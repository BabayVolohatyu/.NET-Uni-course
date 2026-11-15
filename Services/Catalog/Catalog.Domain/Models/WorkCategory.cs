namespace Catalog.Domain.Models;

/// <summary>
/// Lookup / reference table of reconstruction work categories
/// (e.g. Road Repair, Electrical Works, Water Supply).
/// Acts as the "category" side of the M:N relationship with Contractor.
/// </summary>
public class WorkCategory
{
    public int Id { get; private set; }

    /// <summary>Short alphanumeric code used in procurement specs, e.g. "ROAD", "ELEC".</summary>
    public string Code { get; private set; } = "";

    /// <summary>Human-readable category name.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Optional extended description of the category scope.</summary>
    public string? Description { get; private set; }

    // Navigation — M:N junction
    public List<ContractorWorkCategory> ContractorWorkCategories { get; } = new();

    // Required by EF Core
    private WorkCategory() { }

    // Used by HasData seeding (fixed Ids required)
    public WorkCategory(int id, string code, string name, string? description = null)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
    }
}
