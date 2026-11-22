using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Auth;

namespace Payments.Api.Controllers;

/// <summary>
/// Disbursement of reconstruction funds after a tender is awarded.
/// The handout calls this resource payments-api. Manager, user, and guest receive 403 on every route.
/// </summary>
[ApiController]
[Route("payments")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    private static readonly object Gate = new();
    private static readonly List<Disbursement> Items =
    [
        new(1, "UA-2026-KYIV-0142", "ТОВ Шляхбуд", 4_900_000m, "Held"),
        new(2, "UA-2026-MYK-0201", "ТОВ Аквалін", 11_100_000m, "Paid")
    ];

    [HttpGet]
    [RequirePermission("payments:read")]
    public IActionResult List()
    {
        lock (Gate)
            return Ok(Items.ToArray());
    }

    [HttpPost("process")]
    [RequirePermission("payments:process")]
    public IActionResult Process([FromBody] ProcessPaymentRequest? request)
    {
        var row = new Disbursement(
            NextId(),
            request?.TenderReference ?? "UA-2026-NEW",
            request?.ContractorName ?? "Невідомий підрядник",
            request?.Amount ?? 0m,
            "Paid");

        lock (Gate)
            Items.Add(row);

        return Ok(row);
    }

    [HttpPost("refund")]
    [RequirePermission("payments:refund")]
    public IActionResult Refund([FromBody] RefundPaymentRequest? request)
    {
        lock (Gate)
        {
            var row = Items.FirstOrDefault(p => p.Id == request?.PaymentId);
            if (row is null)
                return NotFound(new { title = "Disbursement was not found." });

            var updated = row with { Status = "Refunded" };
            Items[Items.IndexOf(row)] = updated;
            return Ok(updated);
        }
    }

    private static long NextId()
    {
        lock (Gate)
            return Items.Count == 0 ? 1 : Items.Max(p => p.Id) + 1;
    }
}

public sealed record Disbursement(long Id, string TenderReference, string ContractorName, decimal Amount, string Status);

public sealed class ProcessPaymentRequest
{
    public string? TenderReference { get; set; }
    public string? ContractorName { get; set; }
    public decimal Amount { get; set; }
}

public sealed class RefundPaymentRequest
{
    public long PaymentId { get; set; }
}
