using System.Data;
using System.Diagnostics;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Platform.EventBus;
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
///
/// Lab 05: ActivitySource creates custom spans for key business operations.
/// These spans appear in Aspire Dashboard "Traces" under the tenders-api service.
/// </summary>
public sealed class TenderService : ITenderService
{
    // ActivitySource name must match the one registered in ServiceDefaults.ConfigureOpenTelemetry
    // (.AddSource(builder.Environment.ApplicationName) — ApplicationName = "tenders-api")
    private static readonly ActivitySource _activitySource = new("Tenders.Api");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<TenderService> _logger;
    private readonly IEventPublisher _publisher;
    private readonly ICorrelationContext _correlation;

    public TenderService(
        IUnitOfWork uow,
        IMapper mapper,
        ILogger<TenderService> logger,
        IEventPublisher publisher,
        ICorrelationContext correlation)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _publisher = publisher;
        _correlation = correlation;
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
        // Custom span for the CreateTender business operation (visible in Aspire Dashboard Traces)
        using var activity = _activitySource.StartActivity("CreateTender");
        activity?.SetTag("tender.procuring_entity_id", dto.ProcuringEntityId);
        activity?.SetTag("tender.expected_budget", dto.ExpectedBudget);
        activity?.SetTag("tender.category_code", dto.CategoryCode);

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

        activity?.SetTag("tender.id", newId);
        activity?.SetTag("tender.status", TenderStatus.Draft);

        // Structured log with business-specific properties (no sensitive data)
        _logger.LogInformation(
            "Tender created: TenderId={TenderId} Title={Title} " +
            "ProcuringEntityId={ProcuringEntityId} Budget={ExpectedBudget} Status={Status}",
            newId, dto.Title, dto.ProcuringEntityId, dto.ExpectedBudget, TenderStatus.Draft);

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

        // Custom span for the status transition business event
        using var activity = _activitySource.StartActivity("ChangeTenderStatus");
        activity?.SetTag("tender.id", tenderId);
        activity?.SetTag("tender.new_status", newStatus);
        activity?.SetTag("tender.change_reason", reason ?? "not provided");

        var tender = await _uow.Tenders.GetByIdAsync(tenderId, ct)
            ?? throw new NotFoundException($"Tender {tenderId} was not found.");
        var oldStatus = tender.Status;
        var title = tender.Title;

        await _uow.Tenders.ChangeStatusAsync(tenderId, newStatus, reason: reason, ct: ct);

        // The stored procedure has committed. Publish only after that succeeds.
        await _publisher.PublishAsync(
            new TenderStatusChangedEvent(tenderId, title, oldStatus, newStatus, reason)
            {
                CorrelationId = _correlation.Current
            },
            MessagingTopology.TendersExchange,
            MessagingTopology.TenderStatusRoutingKey,
            ct);

        // Structured business event log — auditors can search by TenderId and Status
        _logger.LogInformation(
            "Tender status changed: TenderId={TenderId} OldStatus={OldStatus} NewStatus={NewStatus} Reason={Reason} CorrelationId={CorrelationId}",
            tenderId, oldStatus, newStatus, reason ?? "", _correlation.Current);
    }

    public async Task<IReadOnlyList<TenderStatusHistoryDto>> GetStatusHistoryAsync(
        long tenderId, CancellationToken ct = default)
    {
        var history = await _uow.Tenders.GetStatusHistoryAsync(tenderId, ct);
        return _mapper.Map<IReadOnlyList<TenderStatusHistoryDto>>(history);
    }
}
