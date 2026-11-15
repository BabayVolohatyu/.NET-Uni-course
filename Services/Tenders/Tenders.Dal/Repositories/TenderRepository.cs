using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;
using Tenders.Domain.Exceptions;
using Tenders.Domain.Models;

namespace Tenders.Dal.Repositories;

/// <summary>
/// Tender repository implemented with ADO.NET + Dapper.
/// Inherits generic CRUD from BaseDapperRepository&lt;Tender&gt; and adds:
///  - GetWithDetailsAsync: single-query Dapper multi-mapping (Tender + TenderDetails)
///  - GetByProcuringEntityAsync: calls dbo.usp_GetTendersForEntity stored procedure
///  - ChangeStatusAsync: calls dbo.usp_ChangeTenderStatus stored procedure
///  - GetStatusHistoryAsync: parameterised SELECT with Dapper
/// </summary>
public sealed class TenderRepository : BaseDapperRepository<Tender>, ITenderRepository
{
    public TenderRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Tenders") { }

    // ── AddAsync override ───────────────────────────────────────────────────────

    public override async Task<long> AddAsync(Tender entity, CancellationToken ct = default)
    {
        // Calls usp_CreateTender to insert Tender + TenderDetails atomically.
        await EnsureOpenAsync(ct);
        try
        {
            var p = new DynamicParameters();
            p.Add("@ProcuringEntityId", entity.ProcuringEntityId);
            p.Add("@Title",            entity.Title);
            p.Add("@ExpectedBudget",   entity.ExpectedBudget);
            p.Add("@Description",      entity.Details?.Description ?? string.Empty);
            p.Add("@CategoryCode",     entity.Details?.CategoryCode);
            p.Add("@DeliveryAddress",  entity.Details?.DeliveryAddress ?? string.Empty);
            p.Add("@TechnicalSpecUrl", entity.Details?.TechnicalSpecUrl);
            p.Add("@CreatedBy",        entity.CreatedBy);
            p.Add("@NewTenderId",      dbType: DbType.Int64, direction: ParameterDirection.Output);

            await Connection.ExecuteAsync(
                new CommandDefinition("dbo.usp_CreateTender", p, Transaction,
                    commandType: CommandType.StoredProcedure, cancellationToken: ct));

            return p.Get<long>("@NewTenderId");
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            throw new NotFoundException(ex.Message, ex);
        }
    }

    // ── GetWithDetailsAsync ─────────────────────────────────────────────────────

    public async Task<Tender?> GetWithDetailsAsync(long tenderId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT t.Id, t.ProcuringEntityId, t.Title, t.ExpectedBudget, t.Status,
                   t.PublishedAt, t.BiddingDeadline,
                   t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy, t.IsDeleted,
                   -- TenderDetails columns (splitOn = TenderId)
                   td.TenderId, td.Description, td.CategoryCode, td.DeliveryAddress,
                   td.TechnicalSpecUrl, td.CreatedAt, td.UpdatedAt
            FROM dbo.Tenders t
            LEFT JOIN dbo.TenderDetails td ON td.TenderId = t.Id
            WHERE t.Id = @TenderId AND t.IsDeleted = 0";

        await EnsureOpenAsync(ct);

        Tender? result = null;
        await Connection.QueryAsync<Tender, TenderDetails?, Tender>(
            new CommandDefinition(sql, new { TenderId = tenderId }, Transaction,
                cancellationToken: ct),
            (tender, details) =>
            {
                result ??= tender;
                result.Details = details;
                return result;
            },
            splitOn: "TenderId");

        return result;
    }

    // ── GetByProcuringEntityAsync ───────────────────────────────────────────────

    public async Task<IReadOnlyList<Tender>> GetByProcuringEntityAsync(
        long procuringEntityId, string? status = null, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<Tender>(
            new CommandDefinition("dbo.usp_GetTendersForEntity",
                new { ProcuringEntityId = procuringEntityId, Status = status },
                Transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        return rows.AsList();
    }

    // ── ChangeStatusAsync ───────────────────────────────────────────────────────

    public async Task ChangeStatusAsync(long tenderId, string newStatus,
        string? changedBy = null, string? reason = null, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(
                new CommandDefinition("dbo.usp_ChangeTenderStatus",
                    new { TenderId = tenderId, NewStatus = newStatus, ChangedBy = changedBy, Reason = reason },
                    Transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            throw new NotFoundException(ex.Message, ex);
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            throw new BusinessConflictException(ex.Message, ex);
        }
    }

    // ── GetStatusHistoryAsync ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<TenderStatusHistory>> GetStatusHistoryAsync(
        long tenderId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, TenderId, FromStatus, ToStatus, ChangedAt, ChangedBy, Reason
            FROM dbo.TenderStatusHistory
            WHERE TenderId = @TenderId
            ORDER BY ChangedAt DESC";

        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<TenderStatusHistory>(
            new CommandDefinition(sql, new { TenderId = tenderId }, Transaction,
                cancellationToken: ct));
        return rows.AsList();
    }
}
