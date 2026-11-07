using System.ComponentModel.DataAnnotations;

namespace Management.Application.DTOs;

public sealed class RegisterRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = "";
    [Required, StringLength(256)] public string UserName { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
}
public sealed class LoginRequest
{
    [Required, StringLength(256)] public string UserName { get; init; } = "";
    [Required, StringLength(128)] public string Password { get; init; } = "";
}
public sealed class CreateUserRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = "";
    [Required, StringLength(256)] public string UserName { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
    [Required, MaxLength(100)] public string[] Roles { get; init; } = [];
}
public sealed class UpdateUserRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = "";
    [Required, StringLength(256)] public string UserName { get; init; } = "";
    [Required, MaxLength(100)] public string[] Roles { get; init; } = [];
}
public sealed class RoleRequest
{
    [Required, StringLength(256)] public string Name { get; init; } = "";
    [Required, MaxLength(100)] public string[] Permissions { get; init; } = [];
}
public sealed class UserFilter
{
    [StringLength(256)] public string? Role { get; init; }
    [StringLength(256)] public string? Name { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 10;
}
public sealed class RefreshRequest
{
    [StringLength(256)] public string? RefreshToken { get; init; }
}
