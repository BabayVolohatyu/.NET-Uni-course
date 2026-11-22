using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Auth;

/// <summary>
/// Marks an endpoint with the permission it requires. The policy of the same name
/// is registered in the service's <c>Program.cs</c> (tables 3.3.1–3.3.3).
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(policy: permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
