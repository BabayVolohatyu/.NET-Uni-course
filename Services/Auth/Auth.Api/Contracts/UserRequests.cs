using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed class UpdateUserRequest
{
    [EmailAddress]
    public string? Email { get; set; }

    public string? DisplayName { get; set; }
}

public sealed class AssignRoleRequest
{
    [Required]
    public string Role { get; set; } = "";
}
