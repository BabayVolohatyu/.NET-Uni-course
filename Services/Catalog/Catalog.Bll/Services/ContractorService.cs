using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Queries;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

/// <summary>
/// Business-logic service for Contractor operations.
/// Works exclusively through IUnitOfWork + repositories — never touches EF Core or DbContext directly.
///
/// Responsibilities:
///   - Rules requiring DB data (e.g. EDRPOU uniqueness) → checked here, throw ConflictException
///   - Domain rules (capital > 0, valid EDRPOU format) → enforced by entity constructors/methods
///   - Pagination/filter/sort queries → delegated to Specification + repository
/// </summary>
public sealed class ContractorService : IContractorService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<ContractorService> _logger;

    public ContractorService(IUnitOfWork uow, IMapper mapper, ILogger<ContractorService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    // ── List (paged) ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<ContractorDto>> GetListAsync(
        ContractorQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Count spec has no Skip/Take so COUNT(*) hits only filters, not pagination
        var countSpec = new ContractorsCountSpec(query);
        var total = await _uow.Contractors.CountAsync(countSpec, ct);

        // Data spec includes pagination and includes
        var dataSpec = new ContractorsFilterSpec(query, pageSize);
        var contractors = await _uow.Contractors.ListAsync(dataSpec, ct);

        var items = _mapper.Map<IReadOnlyList<ContractorDto>>(contractors);
        return new PagedResult<ContractorDto>(items, total, query.Page, pageSize);
    }

    // ── Single (with details — Eager Loading) ────────────────────────────────────
    public async Task<ContractorWithDetailsDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);
        return _mapper.Map<ContractorWithDetailsDto>(contractor);
    }

    // ── Create ───────────────────────────────────────────────────────────────────
    public async Task<ContractorDto> CreateAsync(CreateContractorDto input, CancellationToken ct = default)
    {
        // Service-layer guard: DB uniqueness before the domain entity is even built
        if (await _uow.Contractors.ExistsByEdrpouAsync(input.EDRPOU, ct))
            throw new ConflictException(
                $"A contractor with EDRPOU '{input.EDRPOU}' already exists.");

        // Domain entity enforces internal invariants (capital > 0, EDRPOU format)
        var contractor = new Contractor(
            input.Name, input.EDRPOU, input.AuthorizedCapital, input.RegistrationDate);

        await _uow.Contractors.AddAsync(contractor, ct);
        await _uow.SaveChangesAsync(ct);    // flush to get the generated Id

        _logger.LogInformation("Contractor created: Id={Id}, EDRPOU={EDRPOU}", contractor.Id, contractor.EDRPOU);
        return _mapper.Map<ContractorDto>(contractor);
    }

    // ── Update ───────────────────────────────────────────────────────────────────
    public async Task<ContractorDto> UpdateAsync(
        int id, UpdateContractorDto input, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        // Domain methods enforce invariants
        contractor.Rename(input.Name);
        contractor.UpdateCapital(input.AuthorizedCapital);

        _uow.Contractors.Update(contractor);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Contractor updated: Id={Id}", id);
        return _mapper.Map<ContractorDto>(contractor);
    }

    // ── Delete ───────────────────────────────────────────────────────────────────
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        _uow.Contractors.Delete(contractor);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Contractor deleted: Id={Id}", id);
    }

    // ── Activate / Deactivate ────────────────────────────────────────────────────
    public async Task ActivateAsync(int id, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        contractor.Activate();      // domain method guards the transition
        _uow.Contractors.Update(contractor);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        contractor.Deactivate();
        _uow.Contractors.Update(contractor);
        await _uow.SaveChangesAsync(ct);
    }
}
