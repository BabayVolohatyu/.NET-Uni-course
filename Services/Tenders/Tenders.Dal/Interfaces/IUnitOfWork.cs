using System.Data;

namespace Tenders.Dal.Interfaces;

/// <summary>
/// Coordinates multiple repositories inside a single database connection and transaction.
/// One UoW instance = one SqlConnection = one HTTP request (registered as Scoped).
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IProcuringEntityRepository ProcuringEntities { get; }
    ITenderRepository Tenders { get; }
    IBidRepository Bids { get; }
    IContractorRepository Contractors { get; }
    IProcessedEventStore ProcessedEvents { get; }

    /// <summary>Opens the connection and begins a transaction with the specified isolation level.</summary>
    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
