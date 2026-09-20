using System.Data;
using Dapper;
using SchemaHunter.Procurement.Domain.Commands;
using SchemaHunter.Procurement.Domain.DTOs;
using SchemaHunter.Procurement.Infrastructure;

namespace SchemaHunter.Procurement.Infrastructure.Repositories;

/// <summary>
/// Repository for the <c>procurement.tenders</c> and related tables.
/// All data access goes through stored procedures — no inline SQL.
/// </summary>
public sealed class TenderRepository
{
    private readonly ProcurementConnectionFactory _factory;

    public TenderRepository(ProcurementConnectionFactory factory)
        => _factory = factory;

    // ─── §5.4  Upsert tender ──────────────────────────────────────────────────
    /// <summary>
    /// Creates or updates a tender identified by its Prozorro ID.
    /// Returns the internal <c>tender_id</c> of the affected row.
    /// </summary>
    public async Task<long> UpsertAsync(UpsertTenderCommand cmd, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("@ProzorroId",      cmd.ProzorroId);
        parameters.Add("@ProjectCode",     cmd.ProjectCode);
        parameters.Add("@Title",           cmd.Title);
        parameters.Add("@ProcuringEntity", cmd.ProcuringEntity);
        parameters.Add("@ExpectedValue",   cmd.ExpectedValue);
        parameters.Add("@Currency",        cmd.Currency);
        parameters.Add("@Status",          cmd.Status);
        parameters.Add("@PublishedAt",     cmd.PublishedAt);
        parameters.Add("@DeadlineAt",      cmd.DeadlineAt);
        parameters.Add("@TenderId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(
            new CommandDefinition(
                "procurement.usp_UpsertTender",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

        return parameters.Get<long>("@TenderId");
    }

    // ─── §5.5  Paged tender list ──────────────────────────────────────────────
    /// <summary>
    /// Returns a keyset-paginated page of tenders with their current risk assessment,
    /// filtered by optional <paramref name="status"/> and <paramref name="projectCode"/>.
    /// </summary>
    public async Task<IReadOnlyList<TenderPageItem>> GetPagedAsync(
        string?  status,
        string?  projectCode,
        long     afterTenderId,
        int      pageSize,
        CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        var result = await conn.QueryAsync<TenderPageItem>(
            new CommandDefinition(
                "procurement.usp_GetTendersPaged",
                new { Status = status, ProjectCode = projectCode, AfterTenderId = afterTenderId, PageSize = pageSize },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

        return result.AsList();
    }

    // ─── §5.6  Risk report (multiple result sets) ─────────────────────────────
    /// <summary>
    /// Returns the aggregated risk summary and tender detail rows for a project code.
    /// Calls <c>usp_GetRiskReportByProject</c> which returns two result sets.
    /// </summary>
    public async Task<(RiskReportSummary Summary, IReadOnlyList<RiskTenderRow> Tenders)>
        GetRiskReportAsync(string projectCode, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(
                "procurement.usp_GetRiskReportByProject",
                new { ProjectCode = projectCode },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

        var summary = await multi.ReadSingleAsync<RiskReportSummary>();
        var tenders = (await multi.ReadAsync<RiskTenderRow>()).AsList();

        return (summary, tenders);
    }

    // ─── Compute risk assessment for a tender ────────────────────────────────
    /// <summary>
    /// Triggers risk evaluation rules for <paramref name="tenderId"/> and persists
    /// the result. Returns the internal <c>risk_assessment_id</c>.
    /// </summary>
    public async Task<long> ComputeRiskAsync(long tenderId, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("@TenderId",         tenderId);
        parameters.Add("@RiskAssessmentId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(
            new CommandDefinition(
                "procurement.usp_ComputeRiskAssessment",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

        return parameters.Get<long>("@RiskAssessmentId");
    }

    // ─── Submit bid ───────────────────────────────────────────────────────────
    /// <summary>
    /// Registers a bid and recalculates the supplier risk score.
    /// Returns the new <c>bid_id</c>.
    /// </summary>
    public async Task<long> SubmitBidAsync(SubmitBidCommand cmd, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        var parameters = new DynamicParameters();
        parameters.Add("@TenderId",      cmd.TenderId);
        parameters.Add("@SupplierId",    cmd.SupplierId);
        parameters.Add("@OfferedAmount", cmd.OfferedAmount);
        parameters.Add("@SubmittedAt",   cmd.SubmittedAt);
        parameters.Add("@BidId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(
            new CommandDefinition(
                "procurement.usp_SubmitBid",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

        return parameters.Get<long>("@BidId");
    }
}
