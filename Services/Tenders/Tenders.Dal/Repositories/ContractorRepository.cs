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
        const string sql = """
            SELECT Id, Name, EDRPOU, AuthorizedCapital, LastNameEventAt, LastCapitalEventAt, CreatedAt, UpdatedAt
            FROM dbo.Contractors WHERE EDRPOU = @EDRPOU
            """;
        await EnsureOpenAsync(ct);
        return await Connection.QueryFirstOrDefaultAsync<Contractor>(
            new CommandDefinition(sql, new { EDRPOU = edrpou }, Transaction, cancellationToken: ct));
    }

    public async Task<int> ApplyCatalogSnapshotAsync(
        long id,
        string name,
        string edrpou,
        decimal? authorizedCapital,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        // Bids.ContractorName, Bids.ContractorEDRPOU, and Bids.ProposedPrice are snapshots
        // taken at submission. This statement never writes to dbo.Bids.
        const string sql = """
            DECLARE @applied INT = 0;
            IF EXISTS (SELECT 1 FROM dbo.Contractors WHERE Id = @Id)
            BEGIN
                UPDATE dbo.Contractors
                SET Name = CASE
                        WHEN @UpdateName = 1 AND (LastNameEventAt IS NULL OR LastNameEventAt < @OccurredAt) THEN @Name
                        ELSE Name END,
                    EDRPOU = CASE
                        WHEN @UpdateName = 1 AND (LastNameEventAt IS NULL OR LastNameEventAt < @OccurredAt) THEN @Edrpou
                        ELSE EDRPOU END,
                    LastNameEventAt = CASE
                        WHEN @UpdateName = 1 AND (LastNameEventAt IS NULL OR LastNameEventAt < @OccurredAt) THEN @OccurredAt
                        ELSE LastNameEventAt END,
                    AuthorizedCapital = CASE
                        WHEN @AuthorizedCapital IS NOT NULL AND (LastCapitalEventAt IS NULL OR LastCapitalEventAt < @OccurredAt) THEN @AuthorizedCapital
                        ELSE AuthorizedCapital END,
                    LastCapitalEventAt = CASE
                        WHEN @AuthorizedCapital IS NOT NULL AND (LastCapitalEventAt IS NULL OR LastCapitalEventAt < @OccurredAt) THEN @OccurredAt
                        ELSE LastCapitalEventAt END,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id
                  AND (
                        (@UpdateName = 1 AND (LastNameEventAt IS NULL OR LastNameEventAt < @OccurredAt))
                     OR (@AuthorizedCapital IS NOT NULL AND (LastCapitalEventAt IS NULL OR LastCapitalEventAt < @OccurredAt))
                  );
                SET @applied = @@ROWCOUNT;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.Contractors
                    (Id, Name, EDRPOU, AuthorizedCapital, LastNameEventAt, LastCapitalEventAt)
                VALUES (
                    @Id, @Name, @Edrpou, @AuthorizedCapital,
                    CASE WHEN @UpdateName = 1 THEN @OccurredAt END,
                    CASE WHEN @AuthorizedCapital IS NOT NULL THEN @OccurredAt END);
                SET @applied = 1;
            END
            SELECT @applied;
            """;

        await EnsureOpenAsync(ct);
        return await Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql,             new
            {
                Id = id,
                Name = name,
                Edrpou = edrpou,
                AuthorizedCapital = authorizedCapital,
                UpdateName = string.IsNullOrWhiteSpace(name) ? 0 : 1,
                OccurredAt = occurredAt
            }, Transaction, cancellationToken: ct));
    }

    public async Task<int> TryRemoveUnreferencedAsync(long id, CancellationToken ct = default)
    {
        const string sql = """
            DELETE FROM dbo.Contractors
            WHERE Id = @Id
              AND NOT EXISTS (
                    SELECT 1 FROM dbo.Bids
                    WHERE ContractorId = @Id AND IsDeleted = 0);
            SELECT @@ROWCOUNT;
            """;

        await EnsureOpenAsync(ct);
        return await Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
    }
}
