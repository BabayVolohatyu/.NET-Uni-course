namespace Tenders.Domain.Models;

/// <summary>
/// Local read-model snapshot of a contractor from the ContractorRegistry context.
/// ContractorId matches the Id in ContractorRegistry DB — stored as a plain column, NO FOREIGN KEY.
/// Name and EDRPOU are denormalised snapshot fields updated by domain events from Registry.
/// </summary>
public class Contractor
{
    public long Id { get; set; }

    /// <summary>Denormalised name from Registry (kept up-to-date via domain events).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ukrainian 8-digit business code (denormalised from Registry).</summary>
    public string EDRPOU { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
