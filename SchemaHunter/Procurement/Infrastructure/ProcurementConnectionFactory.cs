using Microsoft.Data.SqlClient;

namespace SchemaHunter.Procurement.Infrastructure;

/// <summary>
/// Factory that creates a fresh <see cref="SqlConnection"/> per unit-of-work.
/// The connection string is injected at startup from configuration /
/// the PROCUREMENT_CONNECTION_STRING environment variable.
/// </summary>
public sealed class ProcurementConnectionFactory
{
    private readonly string _connectionString;

    public ProcurementConnectionFactory(string connectionString)
        => _connectionString = connectionString;

    /// <summary>Creates (but does NOT open) a new <see cref="SqlConnection"/>.</summary>
    public SqlConnection Create() => new(_connectionString);
}
