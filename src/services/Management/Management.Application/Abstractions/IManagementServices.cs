using Management.Application.DTOs;

namespace Management.Application.Abstractions;

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(UserFilter filter, CancellationToken ct);
    Task<UserDto> GetAsync(int id, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<ProfileDto> ProfileAsync(int id, CancellationToken ct);
    Task<AuthenticatedUserDto> GetAuthenticationAsync(int id, CancellationToken ct);
}
public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct);
    Task<RoleDto> GetAsync(int id, CancellationToken ct);
    Task<RoleDto> CreateAsync(RoleRequest request, CancellationToken ct);
    Task<RoleDto> UpdateAsync(int id, RoleRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PermissionDto>> PermissionsAsync(CancellationToken ct);
}
public interface IPermissionService
{
    Task<bool> HasAsync(int userId, string permission, CancellationToken ct);
    Task<string[]> GetAsync(int userId, CancellationToken ct);
    Task<IReadOnlyDictionary<int, string[]>> GetForUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken ct);
}
public interface IProfileImageService
{
    Task<ProfileImageData> GetAsync(string fileName, CancellationToken ct);
    Task<string> UploadAsync(int userId, Stream image, long length, CancellationToken ct);
    Task DeleteAsync(int userId, CancellationToken ct);
}
