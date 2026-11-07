using Management.Application.Abstractions;
using Management.Application.Common;
using Management.Application.DTOs;
using Management.Infrastructure.Identity;
using Management.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermissionNames = Management.Application.Authorization.Permissions;

namespace Management.Infrastructure.Services;

public sealed class RoleService(ManagementDbContext db, RoleManager<ApplicationRole> roles) : IRoleService
{
    private IQueryable<RoleDto> Query(int? id = null) => db.Roles.AsNoTracking()
        .Where(r => id == null || r.Id == id).OrderBy(r => r.Id).Select(r => new RoleDto(r.Id, r.Name!,
        (from claim in db.RoleClaims
         where claim.RoleId == r.Id && claim.ClaimType == PermissionNames.ClaimType &&
             PermissionNames.All.Contains(claim.ClaimValue!)
         orderby claim.ClaimValue
         select claim.ClaimValue!).ToArray()));

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct) => await Query().ToListAsync(ct);
    public async Task<RoleDto> GetAsync(int id, CancellationToken ct) =>
        await Query(id).SingleOrDefaultAsync(ct) ?? throw ApiException.NotFound("Role");

    public async Task<RoleDto> CreateAsync(RoleRequest request, CancellationToken ct)
    {
        var permissions = ResolvePermissions(request.Permissions);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = new ApplicationRole { Name = request.Name.Trim() };
        IdentityErrors.EnsureSuccess(await roles.CreateAsync(role));
        db.RoleClaims.AddRange(permissions.Select(name => new IdentityRoleClaim<int>
        {
            RoleId = role.Id,
            ClaimType = PermissionNames.ClaimType,
            ClaimValue = name
        }));
        await db.SaveChangesAsync(ct);
        var result = await GetAsync(role.Id, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<RoleDto> UpdateAsync(int id, RoleRequest request, CancellationToken ct)
    {
        var permissions = ResolvePermissions(request.Permissions);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = await roles.FindByIdAsync(id.ToString()) ?? throw ApiException.NotFound("Role");
        if (role.IsBootstrapAdmin && (request.Name.Trim() != "Admin" ||
            PermissionNames.All.Except(request.Permissions, StringComparer.Ordinal).Any()))
            throw ApiException.Conflict("The bootstrap Admin role must retain its name and all predefined permissions.");
        role.Name = request.Name.Trim();
        // Identity's concurrency stamp prevents simultaneous permission edits from silently overwriting each other.
        IdentityErrors.EnsureSuccess(await roles.UpdateAsync(role));
        await db.RoleClaims.Where(c => c.RoleId == id && c.ClaimType == PermissionNames.ClaimType).ExecuteDeleteAsync(ct);
        db.RoleClaims.AddRange(permissions.Select(name => new IdentityRoleClaim<int>
        {
            RoleId = id,
            ClaimType = PermissionNames.ClaimType,
            ClaimValue = name
        }));
        await db.SaveChangesAsync(ct);
        var result = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var role = await roles.FindByIdAsync(id.ToString()) ?? throw ApiException.NotFound("Role");
        if (role.IsBootstrapAdmin) throw ApiException.Conflict("The bootstrap Admin role cannot be deleted.");
        IdentityErrors.EnsureSuccess(await roles.DeleteAsync(role));
    }

    public Task<IReadOnlyList<PermissionDto>> PermissionsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<PermissionDto> catalog = PermissionNames.All.Select((name, index) => new PermissionDto(index + 1, name)).ToArray();
        return Task.FromResult(catalog);
    }

    private static string[] ResolvePermissions(string[] names)
    {
        if (names.Any(n => !PermissionNames.All.Contains(n, StringComparer.Ordinal)))
            throw ApiException.Validation("permissions", "One or more permission names are invalid.");
        return names.Distinct(StringComparer.Ordinal).ToArray();
    }
}
