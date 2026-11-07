using Management.Application.Abstractions;
using Management.Application.Authorization;
using Management.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Authorization;

public sealed class PermissionService(ManagementDbContext db) : IPermissionService
{
    private IQueryable<string> Query(int userId) =>
        from ur in db.UserRoles.AsNoTracking()
        join claim in db.RoleClaims on ur.RoleId equals claim.RoleId
        where ur.UserId == userId && claim.ClaimType == Permissions.ClaimType &&
            Permissions.All.Contains(claim.ClaimValue!)
        select claim.ClaimValue!;

    public Task<bool> HasAsync(int userId, string permission, CancellationToken ct) => Query(userId).ContainsAsync(permission, ct);
    public Task<string[]> GetAsync(int userId, CancellationToken ct) => Query(userId).Distinct().OrderBy(x => x).ToArrayAsync(ct);

    public async Task<IReadOnlyDictionary<int, string[]>> GetForUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<int, string[]>();
        var grants = await (
            from membership in db.UserRoles.AsNoTracking()
            join claim in db.RoleClaims on membership.RoleId equals claim.RoleId
            where userIds.Contains(membership.UserId) && claim.ClaimType == Permissions.ClaimType &&
                Permissions.All.Contains(claim.ClaimValue!)
            select new { membership.UserId, Permission = claim.ClaimValue! })
            .Distinct().OrderBy(grant => grant.UserId).ThenBy(grant => grant.Permission).ToListAsync(ct);
        return grants.GroupBy(grant => grant.UserId)
            .ToDictionary(group => group.Key, group => group.Select(grant => grant.Permission).ToArray());
    }
}
