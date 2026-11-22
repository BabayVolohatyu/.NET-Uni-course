using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Auth;

/// <summary>
/// A single permission such as <c>orders:update</c>. The policy name equals the permission
/// so <see cref="RequirePermissionAttribute"/> can select it.
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
