using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Auth;

namespace Catalog.Api.Controllers;

/// <summary>
/// Permission matrix for the handout's Products service (table 3.3.2).
/// In this platform a "product" is a reconstruction work package in the contractor registry.
/// </summary>
[ApiController]
[Route("products")]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private static readonly object Gate = new();
    private static readonly List<WorkPackage> Products =
    [
        new(1, "ROAD", "Дорожнє покриття", false),
        new(2, "BLDG", "Конструктив будівлі", true),
        new(3, "ELEC", "Електромережі", false)
    ];

    [HttpGet]
    [RequirePermission("products:read")]
    public IActionResult List()
    {
        lock (Gate)
            return Ok(Products.ToArray());
    }

    [HttpPost]
    [RequirePermission("products:create")]
    public IActionResult Create([FromBody] UpsertProductRequest? request)
    {
        var created = new WorkPackage(NextId(), request?.Code ?? "NEW", request?.Name ?? "Нова категорія робіт", false);
        lock (Gate)
            Products.Add(created);
        return Ok(created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("products:update")]
    public IActionResult Update(long id, [FromBody] UpsertProductRequest? request)
    {
        lock (Gate)
        {
            var index = Products.FindIndex(p => p.Id == id);
            if (index < 0)
                return NotFound(new { title = "Work package was not found." });

            var current = Products[index];
            var updated = current with { Name = request?.Name ?? current.Name, Code = request?.Code ?? current.Code };
            Products[index] = updated;
            return Ok(updated);
        }
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("products:delete")]
    public IActionResult Delete(long id)
    {
        lock (Gate)
        {
            var removed = Products.RemoveAll(p => p.Id == id);
            return removed == 0 ? NotFound(new { title = "Work package was not found." }) : NoContent();
        }
    }

    [HttpPost("{id:long}/publish")]
    [RequirePermission("products:publish")]
    public IActionResult Publish(long id)
    {
        lock (Gate)
        {
            var index = Products.FindIndex(p => p.Id == id);
            if (index < 0)
                return NotFound(new { title = "Work package was not found." });

            var updated = Products[index] with { Published = true };
            Products[index] = updated;
            return Ok(updated);
        }
    }

    private static long NextId()
    {
        lock (Gate)
            return Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1;
    }
}

public sealed record WorkPackage(long Id, string Code, string Name, bool Published);

public sealed class UpsertProductRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}

/// <summary>Scope policies for IdentityServer tokens against the contractor catalog.</summary>
[ApiController]
[Route("api/products/scopes")]
[Produces("application/json")]
public sealed class ProductScopeController : ControllerBase
{
    [HttpGet("read")]
    [Authorize(Policy = "products.read")]
    public IActionResult Read()
        => Ok(new { scope = "products.read", service = "catalog-api", resource = "products_api" });

    [HttpPost("write")]
    [Authorize(Policy = "products.write")]
    public IActionResult Write()
        => Ok(new { scope = "products.write", service = "catalog-api", resource = "products_api" });
}
