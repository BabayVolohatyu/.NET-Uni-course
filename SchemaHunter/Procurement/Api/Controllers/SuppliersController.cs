using Microsoft.AspNetCore.Mvc;
using SchemaHunter.Procurement.Domain.Commands;
using SchemaHunter.Procurement.Infrastructure.Repositories;

namespace SchemaHunter.Procurement.Api.Controllers;

/// <summary>
/// Manages procurement suppliers.
/// </summary>
[ApiController]
[Route("api/suppliers")]
[Produces("application/json")]
public sealed class SuppliersController : ControllerBase
{
    private readonly SupplierRepository _suppliers;

    public SuppliersController(SupplierRepository suppliers) => _suppliers = suppliers;

    // POST /api/suppliers
    /// <summary>Registers a new supplier.</summary>
    [HttpPost]
    [ProducesResponseType<long>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierCommand cmd,
        CancellationToken ct = default)
    {
        var supplierId = await _suppliers.CreateAsync(cmd, ct);
        return CreatedAtAction(nameof(Create), new { supplierId }, new { supplierId });
    }

    // DELETE /api/suppliers/{supplierId}
    /// <summary>Soft-deletes a supplier and withdraws all their open bids.</summary>
    [HttpDelete("{supplierId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(
        long supplierId,
        CancellationToken ct = default)
    {
        try
        {
            await _suppliers.SoftDeleteAsync(supplierId, ct);
            return NoContent();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 50003)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
