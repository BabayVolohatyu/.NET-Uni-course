using System.Data;
using Microsoft.Data.SqlClient;

namespace Tenders.Dal.Repositories;

/// <summary>
/// Base class shared by all repositories.
/// Repositories receive the connection and a delegate to the current transaction from UnitOfWork.
/// They never open/close the connection themselves — UoW owns the connection lifecycle.
/// </summary>
public abstract class RepositoryBase
{
    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    /// <summary>The current Unit-of-Work transaction (or null if no transaction is active).</summary>
    protected SqlTransaction? Transaction => _currentTransaction();

    /// <summary>Opens the connection if it is not yet open. Idempotent.</summary>
    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }
}
