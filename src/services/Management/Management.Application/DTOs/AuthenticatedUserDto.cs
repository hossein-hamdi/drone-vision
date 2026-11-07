namespace Management.Application.DTOs;

public sealed record AuthenticatedUserDto(int Id, string FullName, string UserName, string? ProfileImageUrl,
    string[] Roles, string[] Permissions);
