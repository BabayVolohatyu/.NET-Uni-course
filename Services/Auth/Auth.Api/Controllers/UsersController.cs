using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Controllers;

/// <summary>
/// Auditor profiles. Admin endpoints require role Admin. Owner endpoints accept the same user id
/// that was issued in the access token, or an Admin.
/// </summary>
[ApiController]
[Route("users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var accounts = await users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        var result = new List<object>(accounts.Count);
        foreach (var account in accounts)
            result.Add(await ToDtoAsync(account));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await ToDtoAsync(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Email) && !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var email = await users.SetEmailAsync(user, request.Email.Trim());
            if (!email.Succeeded)
                return BadRequest(new { errors = email.Errors.Select(e => e.Description) });
            user.EmailConfirmed = false;
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName.Trim();
            var updated = await users.UpdateAsync(user);
            if (!updated.Succeeded)
                return BadRequest(new { errors = updated.Errors.Select(e => e.Description) });
        }

        return Ok(await ToDtoAsync(user));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
            return BadRequest(new { errors = deleted.Errors.Select(e => e.Description) });

        return NoContent();
    }

    [HttpGet("{id}/refresh-tokens")]
    public async Task<IActionResult> RefreshTokens(string id, CancellationToken ct)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var history = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.CreatedAt,
                t.ExpiresAt,
                t.RevokedAt,
                t.ReplacedByToken,
                t.CreatedByIp,
                active = t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow
            })
            .ToListAsync(ct);

        return Ok(history);
    }

    [HttpGet("{id}/roles")]
    public async Task<IActionResult> Roles(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        return Ok(await users.GetRolesAsync(user));
    }

    [HttpPost("{id}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(string id, AssignRoleRequest request)
    {
        if (request.Role is not (IdentitySeeder.UserRole or IdentitySeeder.AdminRole))
            return BadRequest(new { title = "Only User and Admin can be assigned." });

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (await users.IsInRoleAsync(user, request.Role))
            return Ok(await users.GetRolesAsync(user));

        var added = await users.AddToRoleAsync(user, request.Role);
        if (!added.Succeeded)
            return BadRequest(new { errors = added.Errors.Select(e => e.Description) });

        return Ok(await users.GetRolesAsync(user));
    }

    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var removed = await users.RemoveFromRoleAsync(user, role);
        if (!removed.Succeeded)
            return BadRequest(new { errors = removed.Errors.Select(e => e.Description) });

        return Ok(await users.GetRolesAsync(user));
    }

    private bool IsOwnerOrAdmin(string id)
    {
        if (User.IsInRole(IdentitySeeder.AdminRole))
            return true;

        var userId = User.FindFirst("userId")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return string.Equals(userId, id, StringComparison.Ordinal);
    }

    private async Task<object> ToDtoAsync(ApplicationUser user)
        => new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.EmailConfirmed,
            roles = await users.GetRolesAsync(user)
        };
}
