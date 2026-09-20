using System.Data;
using Dapper;
using SchemaHunter.Procurement.Domain.Commands;
using SchemaHunter.Procurement.Infrastructure;

namespace SchemaHunter.Procurement.Infrastructure.Repositories;

/// <summary>
/// Repository for <c>procurement.suppliers</c>.
/// </summary>
public sealed class SupplierRepository
{
    private readonly ProcurementConnectionFactory _factory;

    public SupplierRepository(ProcurementConnectionFactory factory)
        => _factory = factory;

    /// <summary>Inserts a new supplier and returns its generated <c>supplier_id</c>.</summary>
    public async Task<long> CreateAsync(CreateSupplierCommand cmd, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        const string sql = """
            INSERT INTO procurement.suppliers
                (edrpou_code, legal_name, tax_status, registration_date)
            VALUES
                (@EdrpouCode, @LegalName, @TaxStatus, @RegistrationDate);
            SELECT SCOPE_IDENTITY();
            """;

        return await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(sql,
                new
                {
                    cmd.EdrpouCode,
                    cmd.LegalName,
                    cmd.TaxStatus,
                    cmd.RegistrationDate
                },
                cancellationToken: ct));
    }

    /// <summary>
    /// Soft-deletes a supplier and withdraws all open bids.
    /// Delegates to <c>usp_SoftDeleteSupplier</c>.
    /// </summary>
    public async Task SoftDeleteAsync(long supplierId, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();

        await conn.ExecuteAsync(
            new CommandDefinition(
                "procurement.usp_SoftDeleteSupplier",
                new { SupplierId = supplierId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
    }
}
