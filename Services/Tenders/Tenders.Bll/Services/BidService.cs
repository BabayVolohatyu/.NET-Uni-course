using System.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Tenders.Bll.Dtos;
using Tenders.Bll.Interfaces;
using Tenders.Dal.Interfaces;
using Tenders.Domain.Constants;
using Tenders.Domain.Exceptions;

namespace Tenders.Bll.Services;

/// <summary>
/// Implements bid business logic.
/// AwardTenderAsync is the key transactional operation:
///  - Begins a ReadCommitted transaction on the shared UoW connection
///  - Calls BidRepository.AwardTenderAsync → dbo.usp_AwardTender
///    (which marks the winning bid AND changes tender status to Awarded, all in one DB transaction)
///  - Commits on success; rolls back on any exception → both changes are reversed atomically
///
/// This is the critical end-to-end chain: procedure → DAL → BLL → HTTP 200/409/404.
/// </summary>
public sealed class BidService : IBidService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<BidService> _logger;

    public BidService(IUnitOfWork uow, IMapper mapper, ILogger<BidService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BidDto>> GetBidsForTenderAsync(long tenderId, CancellationToken ct = default)
    {
        var bids = await _uow.Bids.GetBidsForTenderAsync(tenderId, ct);
        return _mapper.Map<IReadOnlyList<BidDto>>(bids);
    }

    public async Task<BidDto> SubmitBidAsync(long tenderId, SubmitBidDto dto, CancellationToken ct = default)
    {
        // Business validation: tender must exist and be open for bidding
        var tender = await _uow.Tenders.GetByIdAsync(tenderId, ct)
            ?? throw new NotFoundException($"Tender {tenderId} not found.");

        if (tender.Status != TenderStatus.BiddingOpen)
            throw new BusinessConflictException(
                $"Tender {tenderId} is in '{tender.Status}' status. Bids can only be submitted when status is '{TenderStatus.BiddingOpen}'.");

        // Business validation: contractor must exist in local snapshot
        var contractor = await _uow.Contractors.GetByIdAsync(dto.ContractorId, ct)
            ?? throw new NotFoundException(
                $"Contractor {dto.ContractorId} not found in local registry snapshot. Register the contractor first.");

        // Stored procedure usp_SubmitBid takes contractor snapshot at submission time
        await _uow.Bids.SubmitBidAsync(tenderId, dto.ContractorId, dto.ProposedPrice,
            dto.PriceReductionPct, ct: ct);

        _logger.LogInformation(
            "Bid submitted: Tender={TenderId}, Contractor={ContractorId}, Price={Price}",
            tenderId, dto.ContractorId, dto.ProposedPrice);

        return new BidDto(
            TenderId: tenderId,
            ContractorId: dto.ContractorId,
            ContractorName: contractor.Name,
            ContractorEDRPOU: contractor.EDRPOU,
            ProposedPrice: dto.ProposedPrice,
            PriceReductionPct: dto.PriceReductionPct,
            SubmittedAt: DateTime.UtcNow,
            IsWinner: false,
            IsDisqualified: false);
    }

    /// <summary>
    /// Awards the tender to the specified contractor.
    /// TRANSACTIONAL: begins UoW transaction before calling usp_AwardTender.
    /// The stored procedure marks the winning bid and changes tender status atomically.
    /// On any failure (tender not in correct state, bid not found), rolls back both changes.
    /// </summary>
    public async Task AwardTenderAsync(long tenderId, AwardTenderDto dto, CancellationToken ct = default)
    {
        // Business validation before the transaction
        var tender = await _uow.Tenders.GetByIdAsync(tenderId, ct)
            ?? throw new NotFoundException($"Tender {tenderId} not found.");

        if (tender.Status != TenderStatus.EvaluationInProgress)
            throw new BusinessConflictException(
                $"Tender {tenderId} is in '{tender.Status}' status. Award is only possible from '{TenderStatus.EvaluationInProgress}'.");

        // Open transaction: ReadCommitted prevents dirty reads while evaluation scores are being written
        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // usp_AwardTender: marks bid IsWinner=1 AND sets tender Status=Awarded in one SQL transaction
            // If the bid doesn't exist or is disqualified, the procedure throws 50007/50008 → BusinessConflictException
            await _uow.Bids.AwardTenderAsync(tenderId, dto.WinnerContractorId, ct: ct);
            await _uow.CommitAsync(ct);

            _logger.LogInformation(
                "Tender {TenderId} awarded to Contractor {ContractorId}",
                tenderId, dto.WinnerContractorId);
        }
        catch
        {
            await _uow.RollbackAsync(ct); // tender status and bid winner flag both rolled back
            throw;
        }
    }
}
