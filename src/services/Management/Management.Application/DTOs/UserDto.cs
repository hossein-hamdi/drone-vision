namespace Management.Application.DTOs;

public sealed record UserDto(int Id, string FullName, string UserName, string? ProfileImageUrl,
    string[] Roles, string[] Permissions);
public sealed record ProfileDto(int Id, string FullName, string UserName, string? ProfileImageUrl,
    string[] Roles);
public sealed record RoleDto(int Id, string Name, string[] Permissions);
public sealed record PermissionDto(int Id, string Name);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
