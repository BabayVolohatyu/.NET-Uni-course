using Microsoft.Data.SqlClient;

namespace Tenders.Dal;

/// <summary>
/// Idempotent DDL for Lab 08. Existing ProcurementBidding databases created in Lab 01
/// do not yet have <c>ProcessedEvents</c> or the event-updated contractor columns.
/// </summary>
public sealed class BiddingMessagingSchema(string connectionString)
{
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, """
            IF COL_LENGTH('dbo.Contractors', 'AuthorizedCapital') IS NULL
                ALTER TABLE dbo.Contractors ADD AuthorizedCapital DECIMAL(18,2) NULL;
            """, ct);

        await ExecuteAsync(connection, """
            IF COL_LENGTH('dbo.Contractors', 'LastNameEventAt') IS NULL
                ALTER TABLE dbo.Contractors ADD LastNameEventAt DATETIME2 NULL;
            """, ct);

        await ExecuteAsync(connection, """
            IF COL_LENGTH('dbo.Contractors', 'LastCapitalEventAt') IS NULL
                ALTER TABLE dbo.Contractors ADD LastCapitalEventAt DATETIME2 NULL;
            """, ct);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ProcessedEvents (
                    EventId     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    EventType   NVARCHAR(200)    NOT NULL,
                    ProcessedAt DATETIME2        NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME()
                );
            END
            """, ct);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
