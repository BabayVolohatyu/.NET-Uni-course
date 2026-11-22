using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Queries;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;
using Microsoft.Extensions.Logging;
using Platform.EventBus;
using System.Diagnostics;

namespace Catalog.Bll.Services;

/// <summary>
/// Business-logic service for Contractor operations.
/// Works exclusively through IUnitOfWork + repositories — never touches EF Core or DbContext directly.
///
/// Responsibilities:
///   - Rules requiring DB data (e.g. EDRPOU uniqueness) → checked here, throw ConflictException
///   - Domain rules (capital > 0, valid EDRPOU format) → enforced by entity constructors/methods
///   - Pagination/filter/sort queries → delegated to Specification + repository
///
/// Lab 05: ActivitySource creates custom spans visible in Aspire Dashboard "Traces".
/// </summary>
public sealed class ContractorService : IContractorService
{
    // ActivitySource name matches the service name registered in AppHost ("catalog-api")
    private static readonly ActivitySource _activitySource = new("Catalog.Api");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<ContractorService> _logger;
    private readonly IEventPublisher _publisher;
    private readonly ICorrelationContext _correlation;

    public ContractorService(
        IUnitOfWork uow,
        IMapper mapper,
        ILogger<ContractorService> logger,
        IEventPublisher publisher,
        ICorrelationContext correlation)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _publisher = publisher;
        _correlation = correlation;
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
        // Custom span for contractor registration business operation
        using var activity = _activitySource.StartActivity("RegisterContractor");
        activity?.SetTag("contractor.edrpou", input.EDRPOU);
        activity?.SetTag("contractor.authorized_capital", input.AuthorizedCapital);

        // Service-layer guard: DB uniqueness before the domain entity is even built
        if (await _uow.Contractors.ExistsByEdrpouAsync(input.EDRPOU, ct))
            throw new ConflictException(
                $"A contractor with EDRPOU '{input.EDRPOU}' already exists.");

        // Domain entity enforces internal invariants (capital > 0, EDRPOU format)
        var contractor = new Contractor(
            input.Name, input.EDRPOU, input.AuthorizedCapital, input.RegistrationDate);

        await _uow.Contractors.AddAsync(contractor, ct);
        await _uow.SaveChangesAsync(ct);    // 1) row is committed — only then publish

        activity?.SetTag("contractor.id", contractor.Id);

        await _publisher.PublishAsync(
            new ContractorRegisteredEvent(contractor.Id, contractor.Name, contractor.EDRPOU, contractor.AuthorizedCapital)
            {
                CorrelationId = _correlation.Current
            },
            MessagingTopology.CatalogExchange,
            MessagingTopology.RegisteredRoutingKey,
            ct);

        // Structured business event log — no sensitive data (no passport numbers, contacts)
        _logger.LogInformation(
            "Contractor registered: ContractorId={ContractorId} Name={Name} " +
            "EDRPOU={EDRPOU} AuthorizedCapital={AuthorizedCapital} IsActive={IsActive}",
            contractor.Id, contractor.Name, contractor.EDRPOU,
            contractor.AuthorizedCapital, contractor.IsActive);

        return _mapper.Map<ContractorDto>(contractor);
    }

    // ── Update ───────────────────────────────────────────────────────────────────
    public async Task<ContractorDto> UpdateAsync(
        int id, UpdateContractorDto input, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        var oldName = contractor.Name;
        var oldCapital = contractor.AuthorizedCapital;
        var nameChanged = !string.Equals(oldName, input.Name, StringComparison.Ordinal);
        var capitalChanged = oldCapital != input.AuthorizedCapital;

        // Domain methods enforce invariants
        if (nameChanged)
            contractor.Rename(input.Name);
        if (capitalChanged)
            contractor.UpdateCapital(input.AuthorizedCapital);

        if (!nameChanged && !capitalChanged)
            return _mapper.Map<ContractorDto>(contractor);

        _uow.Contractors.Update(contractor);
        await _uow.SaveChangesAsync(ct);    // 1) committed. A failed save never reaches PublishAsync.

        if (nameChanged)
        {
            await _publisher.PublishAsync(
                new ContractorRenamedEvent(contractor.Id, oldName, contractor.Name, contractor.EDRPOU)
                {
                    CorrelationId = _correlation.Current
                },
                MessagingTopology.CatalogExchange,
                MessagingTopology.RenamedRoutingKey,
                ct);
        }

        if (capitalChanged)
        {
            await _publisher.PublishAsync(
                new ContractorCapitalChangedEvent(
                    contractor.Id, contractor.Name, contractor.EDRPOU, oldCapital, contractor.AuthorizedCapital)
                {
                    CorrelationId = _correlation.Current
                },
                MessagingTopology.CatalogExchange,
                MessagingTopology.CapitalChangedRoutingKey,
                ct);
        }

        _logger.LogInformation(
            "Contractor updated: ContractorId={ContractorId} NameChanged={NameChanged} CapitalChanged={CapitalChanged} CorrelationId={CorrelationId}",
            id, nameChanged, capitalChanged, _correlation.Current);
        return _mapper.Map<ContractorDto>(contractor);
    }

    // ── Delete ───────────────────────────────────────────────────────────────────
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        var name = contractor.Name;
        var edrpou = contractor.EDRPOU;

        _uow.Contractors.Delete(contractor);
        await _uow.SaveChangesAsync(ct);

        await _publisher.PublishAsync(
            new ContractorRemovedEvent(id, name, edrpou)
            {
                CorrelationId = _correlation.Current
            },
            MessagingTopology.CatalogExchange,
            MessagingTopology.RemovedRoutingKey,
            ct);

        _logger.LogInformation(
            "Contractor deleted: ContractorId={ContractorId} CorrelationId={CorrelationId}",
            id, _correlation.Current);
    }

    // ── Activate / Deactivate ────────────────────────────────────────────────────
    public async Task ActivateAsync(int id, CancellationToken ct = default)
    {
        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        contractor.Activate();      // domain method guards the transition
        _uow.Contractors.Update(contractor);
        await _uow.SaveChangesAsync(ct);

        // Business event log: status transitions are auditable in procurement systems
        _logger.LogInformation(
            "Contractor activated: ContractorId={ContractorId} EDRPOU={EDRPOU} Status=Active",
            id, contractor.EDRPOU);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity("DeactivateContractor");
        activity?.SetTag("contractor.id", id);

        var contractor = await _uow.Contractors.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Contractor), id);

        contractor.Deactivate();
        _uow.Contractors.Update(contractor);
        await _uow.SaveChangesAsync(ct);

        // Business event log: deactivation is a risk signal in anomaly detection
        _logger.LogInformation(
            "Contractor deactivated: ContractorId={ContractorId} EDRPOU={EDRPOU} Status=Inactive",
            id, contractor.EDRPOU);
    }
}
