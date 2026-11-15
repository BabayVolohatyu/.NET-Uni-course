using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Models;

/// <summary>
/// A registered private business that submits bids in public reconstruction procurement.
/// Analogue of CatalogItem in the Microsoft eShop reference.
///
/// Business rules are enforced by entity methods (not open setters):
///   - AuthorizedCapital must be > 0 (set in constructor and updated via UpdateCapital)
///   - IsActive transitions guarded by Activate / Deactivate
///   - EDRPOU is immutable after registration (set once in constructor)
/// </summary>
public class Contractor
{
    public int Id { get; private set; }

    /// <summary>Official company name from the state business registry.</summary>
    public string Name { get; private set; } = "";

    /// <summary>
    /// Ukrainian 8-digit Unified State Registry code — unique business identifier.
    /// Immutable after registration; uniqueness enforced at DB and service layer.
    /// </summary>
    public string EDRPOU { get; private set; } = "";

    /// <summary>Registered authorized capital in UAH — must be positive.</summary>
    public decimal AuthorizedCapital { get; private set; }

    /// <summary>Date the company was first registered in the state registry.</summary>
    public DateTime RegistrationDate { get; private set; }

    /// <summary>Whether the contractor is currently eligible to participate in tenders.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────────
    public ContractorDetail? Detail { get; private set; }                        // 1:1
    public List<ContractorDocument> Documents { get; } = new();                  // 1:N
    public List<ContractorWorkCategory> ContractorWorkCategories { get; } = new(); // M:N

    // Required by EF Core — no public use
    private Contractor() { }

    /// <summary>
    /// Creates a new contractor. All business-rule guards run here so the entity
    /// is always valid after construction.
    /// </summary>
    public Contractor(string name, string edrpou, decimal authorizedCapital, DateTime registrationDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Contractor name is required.");
        if (string.IsNullOrWhiteSpace(edrpou) || edrpou.Length != 8 || !edrpou.All(char.IsDigit))
            throw new DomainException("EDRPOU must be exactly 8 numeric digits.");
        if (authorizedCapital <= 0)
            throw new DomainException("Authorized capital must be greater than zero.");

        Name = name;
        EDRPOU = edrpou;
        AuthorizedCapital = authorizedCapital;
        RegistrationDate = registrationDate;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    // ── Business methods ─────────────────────────────────────────────────────────

    /// <summary>Marks the contractor as eligible for tenders. Idempotency guard included.</summary>
    public void Activate()
    {
        if (IsActive) throw new DomainException("Contractor is already active.");
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Suspends the contractor from tender participation.</summary>
    public void Deactivate()
    {
        if (!IsActive) throw new DomainException("Contractor is already inactive.");
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Updates the registered authorized capital. Business rule: must stay positive.</summary>
    public void UpdateCapital(decimal newCapital)
    {
        if (newCapital <= 0)
            throw new DomainException("Authorized capital must be greater than zero.");
        AuthorizedCapital = newCapital;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Updates the company name (e.g. after official rebranding).</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("Contractor name cannot be empty.");
        Name = newName;
        UpdatedAt = DateTime.UtcNow;
    }
}
