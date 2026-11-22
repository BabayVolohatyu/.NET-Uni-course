using System.Data;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;
using Tenders.Domain.Exceptions;
using Tenders.Domain.Models;

namespace Tenders.Dal.Repositories;

/// <summary>
/// Repository implemented in PURE ADO.NET (SqlConnection + SqlCommand + SqlDataReader).
/// Intentionally does NOT inherit IGenericRepository&lt;T&gt; or BaseDapperRepository&lt;T&gt;
/// to demonstrate low-level data access without micro-ORM assistance.
///
/// Security: all values pass through SqlParameters — no string concatenation with user input.
/// Resource management: connections and commands are released via await using.
/// </summary>
public sealed class ProcuringEntityRepository : RepositoryBase, IProcuringEntityRepository
{
    public ProcuringEntityRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<ProcuringEntity?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, Name, EDRPOU, Region, ContactEmail,
                   CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted
            FROM dbo.ProcuringEntities
            WHERE Id = @Id AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapProcuringEntity(reader);
    }

    public async Task<IReadOnlyList<ProcuringEntity>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, Name, EDRPOU, Region, ContactEmail,
                   CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted
            FROM dbo.ProcuringEntities
            WHERE IsDeleted = 0
            ORDER BY Name";

        await EnsureOpenAsync(ct);
        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new List<ProcuringEntity>();
        while (await reader.ReadAsync(ct))
            result.Add(MapProcuringEntity(reader));
        return result;
    }

    public async Task<ProcuringEntity?> GetByEDRPOUAsync(string edrpou, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, Name, EDRPOU, Region, ContactEmail,
                   CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted
            FROM dbo.ProcuringEntities
            WHERE EDRPOU = @EDRPOU AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@EDRPOU", SqlDbType.Char, 8).Value = edrpou;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapProcuringEntity(reader);
    }

    public async Task<long> AddAsync(ProcuringEntity entity, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO dbo.ProcuringEntities
                (Name, EDRPOU, Region, ContactEmail, CreatedBy)
            VALUES
                (@Name, @EDRPOU, @Region, @ContactEmail, @CreatedBy);
            SELECT SCOPE_IDENTITY();";

        await EnsureOpenAsync(ct);
        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@Name",         SqlDbType.NVarChar, 300).Value  = entity.Name;
        cmd.Parameters.Add("@EDRPOU",       SqlDbType.Char,     8).Value    = entity.EDRPOU;
        cmd.Parameters.Add("@Region",       SqlDbType.NVarChar, 100).Value  = entity.Region;
        cmd.Parameters.Add("@ContactEmail", SqlDbType.NVarChar, 200).Value  = (object?)entity.ContactEmail ?? DBNull.Value;
        cmd.Parameters.Add("@CreatedBy",    SqlDbType.NVarChar, 100).Value  = (object?)entity.CreatedBy ?? DBNull.Value;

        try
        {
            var scalar = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(scalar);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new BusinessConflictException(
                $"A procuring entity with EDRPOU '{entity.EDRPOU}' already exists.", ex);
        }
    }

    public async Task<bool> UpdateAsync(ProcuringEntity entity, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.ProcuringEntities
            SET Name         = @Name,
                Region       = @Region,
                ContactEmail = @ContactEmail,
                UpdatedAt    = SYSUTCDATETIME(),
                UpdatedBy    = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@Id",           SqlDbType.BigInt).Value         = entity.Id;
        cmd.Parameters.Add("@Name",         SqlDbType.NVarChar, 300).Value  = entity.Name;
        cmd.Parameters.Add("@Region",       SqlDbType.NVarChar, 100).Value  = entity.Region;
        cmd.Parameters.Add("@ContactEmail", SqlDbType.NVarChar, 200).Value  = (object?)entity.ContactEmail ?? DBNull.Value;
        cmd.Parameters.Add("@UpdatedBy",    SqlDbType.NVarChar, 100).Value  = (object?)entity.UpdatedBy ?? DBNull.Value;

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.ProcuringEntities
            SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND IsDeleted = 0";

        await EnsureOpenAsync(ct);
        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ── private helpers ────────────────────────────────────────────────────────

    private static ProcuringEntity MapProcuringEntity(SqlDataReader r) => new()
    {
        Id           = r.GetInt64(0),
        Name         = r.GetString(1),
        EDRPOU       = r.GetString(2),
        Region       = r.GetString(3),
        ContactEmail = r.IsDBNull(4) ? null : r.GetString(4),
        CreatedAt    = r.GetDateTime(5),
        CreatedBy    = r.IsDBNull(6) ? null : r.GetString(6),
        UpdatedAt    = r.IsDBNull(7) ? null : r.GetDateTime(7),
        UpdatedBy    = r.IsDBNull(8) ? null : r.GetString(8),
        IsDeleted    = r.GetBoolean(9)
    };
}
