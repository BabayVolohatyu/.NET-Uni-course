using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;
using Tenders.Domain.Exceptions;
using Tenders.Domain.Models;

namespace Tenders.Dal.Repositories;

/// <summary>
/// Bid repository — ADO.NET + Dapper.
/// Inherits generic CRUD but overrides Add to use dbo.usp_SubmitBid.
/// GetBidsForTenderAsync uses dbo.usp_GetBidsForTender.
/// AwardTenderAsync uses dbo.usp_AwardTender — changes both Bids and Tenders tables atomically,
/// demonstrating a transactional business operation across two tables in one transaction (UoW).
/// </summary>
public sealed class BidRepository : BaseDapperRepository<Bid>, IBidRepository
{
    public BidRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Bids") { }

    // ── AddAsync (usp_SubmitBid) ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public override async Task<long> AddAsync(Bid entity, CancellationToken ct = default)
    {
        // Delegate to SubmitBidAsync for clean semantics; return 0 as composite key has no single Id.
        await SubmitBidAsync(entity.TenderId, entity.ContractorId, entity.ProposedPrice,
            entity.PriceReductionPct, entity.CreatedBy, ct);
        return 0; // Bids have composite PK — no single generated Id
    }

    // ── GetBidsForTenderAsync ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<Bid>> GetBidsForTenderAsync(long tenderId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Bid>(
            new CommandDefinition("dbo.usp_GetBidsForTender",
                new { TenderId = tenderId },
                Transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        return rows.AsList();
    }

    // ── SubmitBidAsync ──────────────────────────────────────────────────────────

    public async Task SubmitBidAsync(long tenderId, long contractorId, decimal proposedPrice,
        decimal priceReductionPct = 0, string? createdBy = null, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(
                new CommandDefinition("dbo.usp_SubmitBid",
                    new
                    {
                        TenderId = tenderId,
                        ContractorId = contractorId,
                        ProposedPrice = proposedPrice,
                        PriceReductionPct = priceReductionPct,
                        CreatedBy = createdBy
                    },
                    Transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number == 50003 || ex.Number == 50005)
        {
            throw new NotFoundException(ex.Message, ex);
        }
        catch (SqlException ex) when (ex.Number == 50004)
        {
            throw new BusinessConflictException(ex.Message, ex);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new BusinessConflictException("A bid from this contractor already exists for the tender.", ex);
        }
    }

    // ── AwardTenderAsync ────────────────────────────────────────────────────────

    /// <summary>
    /// Marks the winning bid and transitions the tender to Awarded — both changes in one transaction.
    /// This is the key transactional business operation: it modifies dbo.Bids AND dbo.Tenders.
    /// UoW provides the shared connection + transaction; rollback on failure returns both to prior state.
    /// </summary>
    public async Task AwardTenderAsync(long tenderId, long winnerContractorId,
        string? changedBy = null, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(
                new CommandDefinition("dbo.usp_AwardTender",
                    new { TenderId = tenderId, WinnerContractorId = winnerContractorId, ChangedBy = changedBy },
                    Transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number == 50006)
        {
            throw new NotFoundException(ex.Message, ex);
        }
        catch (SqlException ex) when (ex.Number is 50007 or 50008)
        {
            throw new BusinessConflictException(ex.Message, ex);
        }
    }
}
