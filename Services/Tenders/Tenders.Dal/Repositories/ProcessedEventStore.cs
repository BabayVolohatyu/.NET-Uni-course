using Dapper;
using Microsoft.Data.SqlClient;
using Tenders.Dal.Interfaces;

namespace Tenders.Dal.Repositories;

public sealed class ProcessedEventStore : RepositoryBase, IProcessedEventStore
{
    public ProcessedEventStore(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction)
    {
    }

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.ProcessedEvents (EventId, EventType)
            VALUES (@EventId, @EventType);
            """;

        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { EventId = eventId, EventType = eventType },
                Transaction,
                cancellationToken: ct));
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            return false;
        }
    }
}
