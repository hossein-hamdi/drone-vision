using Microsoft.Extensions.Configuration;
using Management.Application.Abstractions;
using Management.Application.Common;
using Management.Application.DTOs;
using Management.Infrastructure.Identity;
using Management.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services;

public sealed class UserService(ManagementDbContext db, UserManager<ApplicationUser> users,
    IPermissionService permissions, IConfiguration configuration) : IUserService
{
    private readonly string publicPathBase = configuration["Gateway:PathBase"]?.TrimEnd('/') ?? string.Empty;

    private IQueryable<UserDto> Project(IQueryable<ApplicationUser> query) => query.Select(u => new UserDto(
        u.Id, u.FullName, u.UserName!, u.ProfileImage == null ? null : publicPathBase + u.ProfileImage.Url,
        (from ur in db.UserRoles
         join r in db.Roles on ur.RoleId equals r.Id
         where ur.UserId == u.Id
         orderby r.Name
         select r.Name!).ToArray(), Array.Empty<string>()));

    public async Task<PagedResult<UserDto>> ListAsync(UserFilter filter, CancellationToken ct)
    {
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || offset > int.MaxValue)
            throw ApiException.Validation("page", "Pagination is outside the supported range.");
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var role = users.NormalizeName(filter.Role.Trim());
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == role)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var name = filter.Name.Trim();
            query = query.Where(u => u.FullName.Contains(name) || u.UserName!.Contains(name));
        }
        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderBy(u => u.Id).Skip((int)offset).Take(filter.PageSize)).ToListAsync(ct);
        var grants = await permissions.GetForUsersAsync(items.Select(user => user.Id).ToArray(), ct);
        var result = items.Select(user => user with
        {
            Permissions = grants.TryGetValue(user.Id, out var names) ? names : []
        }).ToList();
        return new(result, filter.Page, filter.PageSize, total);
    }

    private async Task<UserDto> FindAsync(int id, CancellationToken ct) =>
        await Project(db.Users.AsNoTracking().Where(u => u.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw ApiException.NotFound("User");

    public async Task<UserDto> GetAsync(int id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        return user with { Permissions = await permissions.GetAsync(id, ct) };
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var roles = await ResolveRolesAsync(request.Roles, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser { FullName = request.FullName.Trim(), UserName = request.UserName.Trim() };
        IdentityErrors.EnsureSuccess(await users.CreateAsync(user, request.Password));
        if (roles.Length > 0) IdentityErrors.EnsureSuccess(await users.AddToRolesAsync(user, roles));
        var dto = await GetAsync(user.Id, ct);
        await transaction.CommitAsync(ct);
        return dto;
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        var roles = await ResolveRolesAsync(request.Roles, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await users.FindByIdAsync(id.ToString()) ?? throw ApiException.NotFound("User");
        user.FullName = request.FullName.Trim();
        user.UserName = request.UserName.Trim();
        IdentityErrors.EnsureSuccess(await users.UpdateAsync(user));
        var existing = await users.GetRolesAsync(user);
        var remove = existing.Except(roles, StringComparer.OrdinalIgnoreCase).ToArray();
        var add = roles.Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
        if (remove.Length > 0) IdentityErrors.EnsureSuccess(await users.RemoveFromRolesAsync(user, remove));
        if (add.Length > 0) IdentityErrors.EnsureSuccess(await users.AddToRolesAsync(user, add));
        var dto = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return dto;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw ApiException.NotFound("User");
        IdentityErrors.EnsureSuccess(await users.DeleteAsync(user));
    }

    public async Task<ProfileDto> ProfileAsync(int id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        return new(user.Id, user.FullName, user.UserName, user.ProfileImageUrl, user.Roles);
    }

    public async Task<AuthenticatedUserDto> GetAuthenticationAsync(int id, CancellationToken ct)
    {
        var user = await GetAsync(id, ct);
        return new(user.Id, user.FullName, user.UserName, user.ProfileImageUrl, user.Roles,
            user.Permissions);
    }

    private async Task<string[]> ResolveRolesAsync(string[] names, CancellationToken ct)
    {
        if (names.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 256))
            throw ApiException.Validation("roles", "Role names must be nonempty and at most 256 characters.");
        var normalized = names.Select(n => users.NormalizeName(n.Trim())).Distinct().ToArray();
        var roles = await db.Roles.AsNoTracking().Where(r => normalized.Contains(r.NormalizedName))
            .Select(r => r.Name!).ToArrayAsync(ct);
        if (roles.Length != normalized.Length) throw ApiException.Validation("roles", "One or more roles do not exist.");
        return roles;
    }
}
