using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;

namespace Tenders.Dal.Repositories;

/// <summary>
/// Shared CRUD base for Dapper-backed repositories.
/// Writes the generic operations (GetById, GetAll, Add, Delete) once;
/// concrete subclasses only add domain-specific queries and stored-procedure calls.
///
/// The table name is passed as a constructor constant from code — never from user input —
/// so it is safe to interpolate into SQL text (OWASP SQL Injection Prevention §Parameterized Queries).
/// All value parameters use Dapper's anonymous-object parameterisation (no string concatenation).
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T>
    where T : class
{
    private readonly string _tableName;

    protected BaseDapperRepository(SqlConnection connection,
                                    Func<SqlTransaction?> currentTransaction,
                                    string tableName)
        : base(connection, currentTransaction)
    {
        _tableName = tableName;
    }

    public async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        // Table name from code constant — parameterising it is not possible in SQL.
        var sql = $"SELECT * FROM {_tableName} WHERE Id = @Id";
        return await Connection.QueryFirstOrDefaultAsync<T>(
            new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {_tableName}";
        var result = await Connection.QueryAsync<T>(
            new CommandDefinition(sql, transaction: Transaction, cancellationToken: ct));
        return result.AsList();
    }

    // Add is intentionally left abstract — each concrete repository provides its own INSERT
    // because the column list differs per entity.
    public abstract Task<long> AddAsync(T entity, CancellationToken ct = default);

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"UPDATE {_tableName} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0";
        var rows = await Connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct));
        return rows > 0;
    }
}
