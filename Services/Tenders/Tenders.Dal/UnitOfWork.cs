using System.Data;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;
using Tenders.Dal.Repositories;

namespace Tenders.Dal;

/// <summary>
/// Coordinates all repositories under a single SqlConnection + SqlTransaction.
/// Registered as Scoped in DI so that one HTTP request uses exactly one connection.
///
/// Design decisions documented (per lab requirements):
///  - Isolation level: ReadCommitted (default) — prevents dirty reads while keeping
///    lock contention low. Higher levels (RepeatableRead, Serializable) reduce anomalies
///    but increase the risk of deadlocks under concurrent writes on the same tender rows.
///  - Connection pooling: SqlClient enables pooling by default; we don't set
///    "Pooling=false" in the connection string, so connections are returned to the pool
///    on DisposeAsync — not physically closed.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);

        // All repositories share ONE connection and delegate to the CURRENT transaction.
        // Using a Func<SqlTransaction?> so each repository always reads the latest transaction
        // even if BeginTransactionAsync is called after the repositories are created.
        ProcuringEntities = new ProcuringEntityRepository(_connection, () => _transaction);
        Tenders           = new TenderRepository(_connection, () => _transaction);
        Bids              = new BidRepository(_connection, () => _transaction);
        Contractors       = new ContractorRepository(_connection, () => _transaction);
    }

    public IProcuringEntityRepository ProcuringEntities { get; }
    public ITenderRepository Tenders { get; }
    public IBidRepository Bids { get; }
    public IContractorRepository Contractors { get; }

    /// <summary>
    /// Opens the shared connection (if not yet open) and begins a transaction with the given isolation level.
    /// ReadCommitted is used as the default: it prevents dirty reads while maintaining reasonable throughput.
    /// </summary>
    public async Task BeginTransactionAsync(
        IsolationLevel level = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        _transaction = (SqlTransaction)
            await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _transaction!.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();

        await _connection.DisposeAsync();
    }
}
