using Tenders.Domain.Models;

namespace Tenders.Dal.Interfaces;

/// <summary>
/// Repository for the local Contractor snapshot (plain read-model; no FK to Registry DB).
/// </summary>
public interface IContractorRepository : IGenericRepository<Contractor>
{
    Task<Contractor?> GetByEDRPOUAsync(string edrpou, CancellationToken ct = default);
}
