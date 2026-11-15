using Catalog.Bll.DTOs;
using Catalog.Bll.Queries;

namespace Catalog.Bll.Interfaces;

/// <summary>
/// Business-logic contract for Contractor operations.
/// Controllers depend on this interface — never on DbContext or repositories directly.
/// </summary>
public interface IContractorService
{
    Task<PagedResult<ContractorDto>> GetListAsync(ContractorQuery query, CancellationToken ct = default);
    Task<ContractorWithDetailsDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ContractorDto> CreateAsync(CreateContractorDto input, CancellationToken ct = default);
    Task<ContractorDto> UpdateAsync(int id, UpdateContractorDto input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task ActivateAsync(int id, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);
}
