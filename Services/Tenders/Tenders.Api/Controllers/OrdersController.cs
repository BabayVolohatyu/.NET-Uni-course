using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Auth;

namespace Tenders.Api.Controllers;

/// <summary>
/// Permission matrix for the handout's Orders service (table 3.3.1).
/// In this platform a procurement "order" is a reconstruction tender.
/// </summary>
[ApiController]
[Route("orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private static readonly object Gate = new();
    private static readonly List<ProcurementOrder> Orders =
    [
        new(1, "Ремонт проїжджої частини Хрещатика", "UA-2026-KYIV-0142", "auditor.user", "Published"),
        new(2, "Покрівля школи у Славутичі", "UA-2026-SLAV-0088", "auditor.admin", "Draft"),
        new(3, "Водогін Миколаєва", "UA-2026-MYK-0201", "kyiv.council", "BiddingOpen")
    ];

    [HttpGet]
    [RequirePermission("orders:read")]
    public IActionResult List()
    {
        lock (Gate)
        {
            IEnumerable<ProcurementOrder> visible = Orders;
            // Auth Service clerks (role User) see only tenders they opened.
            // Keycloak roles are lowercase and are enforced by the permission, not by this filter.
            if (User.IsInRole("User") && !User.IsInRole("Admin"))
            {
                var username = User.FindFirst("username")?.Value;
                visible = Orders.Where(o => o.OpenedBy == username);
            }

            return Ok(visible.ToArray());
        }
    }

    [HttpPost]
    [RequirePermission("orders:create")]
    public IActionResult Create([FromBody] CreateOrderRequest? request)
    {
        var username = User.FindFirst("username")?.Value
            ?? User.FindFirst("preferred_username")?.Value
            ?? "unknown";

        var created = new ProcurementOrder(
            NextId(),
            request?.Title ?? "Новий тендер відбудови",
            request?.Reference ?? $"UA-2026-{Random.Shared.Next(1000, 9999)}",
            username,
            "Draft");

        lock (Gate)
            Orders.Add(created);

        return Ok(created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("orders:update")]
    public IActionResult Update(long id, [FromBody] UpdateOrderRequest? request)
    {
        lock (Gate)
        {
            var index = Orders.FindIndex(o => o.Id == id);
            if (index < 0)
                return NotFound(new { title = "Tender was not found." });

            var current = Orders[index];
            var updated = current with { Title = request?.Title ?? current.Title };
            Orders[index] = updated;
            return Ok(updated);
        }
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("orders:delete")]
    public IActionResult Delete(long id)
    {
        lock (Gate)
        {
            var removed = Orders.RemoveAll(o => o.Id == id);
            return removed == 0
                ? NotFound(new { title = "Tender was not found." })
                : NoContent();
        }
    }

    [HttpPost("{id:long}/approve")]
    [RequirePermission("orders:approve")]
    public IActionResult Approve(long id)
    {
        lock (Gate)
        {
            var index = Orders.FindIndex(o => o.Id == id);
            if (index < 0)
                return NotFound(new { title = "Tender was not found." });

            var updated = Orders[index] with { Status = "Approved" };
            Orders[index] = updated;
            return Ok(updated);
        }
    }

    private static long NextId()
    {
        lock (Gate)
            return Orders.Count == 0 ? 1 : Orders.Max(o => o.Id) + 1;
    }
}

public sealed record ProcurementOrder(long Id, string Title, string Reference, string OpenedBy, string Status);

public sealed class CreateOrderRequest
{
    public string? Title { get; set; }
    public string? Reference { get; set; }
}

public sealed class UpdateOrderRequest
{
    public string? Title { get; set; }
}

/// <summary>
/// Scope policies for IdentityServer tokens (orders.read / orders.write).
/// Separate from the Keycloak permission matrix so the two schemes do not block each other.
/// </summary>
[ApiController]
[Route("api/tenders/scopes")]
[Produces("application/json")]
public sealed class TenderScopeController : ControllerBase
{
    [HttpGet("read")]
    [Authorize(Policy = "orders.read")]
    public IActionResult Read()
        => Ok(new { scope = "orders.read", service = "tenders-api", resource = "orders_api" });

    [HttpPost("write")]
    [Authorize(Policy = "orders.write")]
    public IActionResult Write()
        => Ok(new { scope = "orders.write", service = "tenders-api", resource = "orders_api" });
}
