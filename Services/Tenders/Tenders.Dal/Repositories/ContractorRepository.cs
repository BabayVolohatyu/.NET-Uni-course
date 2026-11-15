using Dapper;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;
using Tenders.Domain.Models;

namespace Tenders.Dal.Repositories;

/// <summary>
/// Contractor (local snapshot) repository — ADO.NET + Dapper.
/// Inherits generic CRUD from BaseDapperRepository&lt;Contractor&gt;.
/// </summary>
public sealed class ContractorRepository : BaseDapperRepository<Contractor>, IContractorRepository
{
    public ContractorRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Contractors") { }

    // ── AddAsync override ───────────────────────────────────────────────────────

    public override async Task<long> AddAsync(Contractor entity, CancellationToken ct = default)
    {
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.Contractors WHERE Id = @Id)
                INSERT INTO dbo.Contractors (Id, Name, EDRPOU)
                VALUES (@Id, @Name, @EDRPOU);
            SELECT @Id;";

        await EnsureOpenAsync(ct);
        return await Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql,
                new { entity.Id, entity.Name, entity.EDRPOU },
                Transaction, cancellationToken: ct));
    }

    // ── GetByEDRPOUAsync ────────────────────────────────────────────────────────

    public async Task<Contractor?> GetByEDRPOUAsync(string edrpou, CancellationToken ct = default)
    {
        const string sql = "SELECT Id, Name, EDRPOU, CreatedAt, UpdatedAt FROM dbo.Contractors WHERE EDRPOU = @EDRPOU";
        await EnsureOpenAsync(ct);
        return await Connection.QueryFirstOrDefaultAsync<Contractor>(
            new CommandDefinition(sql, new { EDRPOU = edrpou }, Transaction, cancellationToken: ct));
    }
}
