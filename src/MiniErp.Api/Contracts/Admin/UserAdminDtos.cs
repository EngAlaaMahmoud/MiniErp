using System.ComponentModel.DataAnnotations;
using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Contracts.Admin;

public sealed record UserAdminListItem(
    Guid Id,
    string Name,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public sealed class CreateUserRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(50)]
    public string Pin { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateUserRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;
}

public sealed class ResetPinRequest
{
    [Required]
    [MaxLength(50)]
    public string Pin { get; set; } = "";
}

