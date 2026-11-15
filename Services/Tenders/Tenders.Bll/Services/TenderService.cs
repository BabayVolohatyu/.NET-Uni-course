using System.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Tenders.Bll.Dtos;
using Tenders.Bll.Interfaces;
using Tenders.Dal.Interfaces;
using Tenders.Domain.Constants;
using Tenders.Domain.Exceptions;
using Tenders.Domain.Models;

namespace Tenders.Bll.Services;

/// <summary>
/// Implements tender and procuring-entity business logic.
/// Uses Unit of Work + repositories; never exposes SQL to the API layer.
/// Business invariants are enforced here; DataAnnotations guard the DTO boundary in the API layer.
/// </summary>
public sealed class TenderService : ITenderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<TenderService> _logger;

    public TenderService(IUnitOfWork uow, IMapper mapper, ILogger<TenderService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    // ── ProcuringEntity ─────────────────────────────────────────────────────────

    public async Task<ProcuringEntityDto?> GetProcuringEntityByIdAsync(long id, CancellationToken ct = default)
    {
        var entity = await _uow.ProcuringEntities.GetByIdAsync(id, ct);
        return entity is null ? null : _mapper.Map<ProcuringEntityDto>(entity);
    }

    public async Task<IReadOnlyList<ProcuringEntityDto>> GetAllProcuringEntitiesAsync(CancellationToken ct = default)
    {
        var entities = await _uow.ProcuringEntities.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<ProcuringEntityDto>>(entities);
    }

    public async Task<ProcuringEntityDto> CreateProcuringEntityAsync(
        CreateProcuringEntityDto dto, CancellationToken ct = default)
    {
        // Business-level validation: check EDRPOU uniqueness before INSERT
        // (DB UNIQUE constraint is the final guard; we surface a friendly message here)
        var existing = await _uow.ProcuringEntities.GetByEDRPOUAsync(dto.EDRPOU, ct);
        if (existing is not null)
            throw new BusinessConflictException(
                $"A procuring entity with EDRPOU '{dto.EDRPOU}' already exists (Id = {existing.Id}).");

        var entity = new ProcuringEntity
        {
            Name         = dto.Name,
            EDRPOU       = dto.EDRPOU,
            Region       = dto.Region,
            ContactEmail = dto.ContactEmail
        };

        var newId = await _uow.ProcuringEntities.AddAsync(entity, ct);
        entity.Id = newId;
        _logger.LogInformation("Created ProcuringEntity {Id} ({EDRPOU})", newId, dto.EDRPOU);

        return _mapper.Map<ProcuringEntityDto>(entity);
    }

    // ── Tender ───────────────────────────────────────────────────────────────────

    public async Task<TenderWithDetailsDto?> GetTenderByIdAsync(long id, CancellationToken ct = default)
    {
        var tender = await _uow.Tenders.GetWithDetailsAsync(id, ct);
        return tender is null ? null : _mapper.Map<TenderWithDetailsDto>(tender);
    }

    public async Task<IReadOnlyList<TenderDto>> GetTendersByEntityAsync(
        long procuringEntityId, string? status = null, CancellationToken ct = default)
    {
        var tenders = await _uow.Tenders.GetByProcuringEntityAsync(procuringEntityId, status, ct);
        return _mapper.Map<IReadOnlyList<TenderDto>>(tenders);
    }

    public async Task<TenderWithDetailsDto> CreateTenderAsync(
        CreateTenderDto dto, CancellationToken ct = default)
    {
        // Business validation: procuring entity must exist
        var entity = await _uow.ProcuringEntities.GetByIdAsync(dto.ProcuringEntityId, ct)
            ?? throw new NotFoundException($"ProcuringEntity {dto.ProcuringEntityId} not found.");

        // Business validation: budget must be positive (also guarded by DataAnnotations, but checked here too)
        if (dto.ExpectedBudget <= 0)
            throw new ValidationException("ExpectedBudget must be greater than zero.");

        var tender = new Tender
        {
            ProcuringEntityId = dto.ProcuringEntityId,
            Title             = dto.Title,
            ExpectedBudget    = dto.ExpectedBudget,
            Status            = TenderStatus.Draft,
            Details = new TenderDetails
            {
                Description      = dto.Description,
                CategoryCode     = dto.CategoryCode,
                DeliveryAddress  = dto.DeliveryAddress,
                TechnicalSpecUrl = dto.TechnicalSpecUrl
            }
        };

        var newId = await _uow.Tenders.AddAsync(tender, ct);
        tender.Id = newId;
        tender.Details.TenderId = newId;

        _logger.LogInformation("Created Tender {Id} for ProcuringEntity {EntityId}", newId, dto.ProcuringEntityId);
        return _mapper.Map<TenderWithDetailsDto>(tender);
    }

    /// <summary>
    /// Changes the tender's lifecycle status.
    /// The stored procedure validates allowed transitions and throws THROW 50001 (not found) or 50002 (invalid).
    /// DAL translates those into NotFoundException / BusinessConflictException, which the API maps to 404 / 409.
    /// </summary>
    public async Task ChangeTenderStatusAsync(
        long tenderId, string newStatus, string? reason = null, CancellationToken ct = default)
    {
        // Business-level status enum guard before hitting the DB
        var validStatuses = new[]
        {
            TenderStatus.Published, TenderStatus.BiddingOpen,
            TenderStatus.EvaluationInProgress, TenderStatus.Awarded, TenderStatus.Cancelled
        };
        if (!validStatuses.Contains(newStatus))
            throw new ValidationException($"'{newStatus}' is not a valid tender status.");

        await _uow.Tenders.ChangeStatusAsync(tenderId, newStatus, reason: reason, ct: ct);
        _logger.LogInformation("Tender {Id} status changed to {Status}", tenderId, newStatus);
    }

    public async Task<IReadOnlyList<TenderStatusHistoryDto>> GetStatusHistoryAsync(
        long tenderId, CancellationToken ct = default)
    {
        var history = await _uow.Tenders.GetStatusHistoryAsync(tenderId, ct);
        return _mapper.Map<IReadOnlyList<TenderStatusHistoryDto>>(history);
    }
}
