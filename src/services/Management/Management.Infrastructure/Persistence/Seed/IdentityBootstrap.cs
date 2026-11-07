using Management.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PermissionNames = Management.Application.Authorization.Permissions;

namespace Management.Infrastructure.Persistence.Seed;

public sealed class IdentityBootstrap(ManagementDbContext db, UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles, IConfiguration configuration)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = N'DroneVision.IdentityBootstrap',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;
            IF @result < 0 THROW 51000, 'Unable to acquire the identity bootstrap lock.', 1;
            """, ct);
        var admin = await roles.FindByNameAsync("Admin");
        if (admin is null)
        {
            admin = new ApplicationRole { Name = "Admin", IsBootstrapAdmin = true };
            IdentityErrors.EnsureSuccess(await roles.CreateAsync(admin));
        }
        else if (!admin.IsBootstrapAdmin)
        {
            admin.IsBootstrapAdmin = true;
            IdentityErrors.EnsureSuccess(await roles.UpdateAsync(admin));
        }
        var assigned = await db.RoleClaims.Where(c => c.RoleId == admin.Id && c.ClaimType == PermissionNames.ClaimType)
            .Select(c => c.ClaimValue).ToListAsync(ct);
        db.RoleClaims.AddRange(PermissionNames.All.Except(assigned).Select(name => new IdentityRoleClaim<int>
        {
            RoleId = admin.Id,
            ClaimType = PermissionNames.ClaimType,
            ClaimValue = name
        }));
        var userName = configuration["BootstrapAdmin:UserName"];
        if (string.IsNullOrWhiteSpace(userName)) throw new InvalidOperationException("BootstrapAdmin:UserName is required.");
        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            var password = configuration["BootstrapAdmin:Password"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("First startup requires BootstrapAdmin:Password from User Secrets or BootstrapAdmin__Password.");
            user = new ApplicationUser
            {
                UserName = userName,
                FullName = configuration["BootstrapAdmin:FullName"] ?? "System Administrator"
            };
            IdentityErrors.EnsureSuccess(await users.CreateAsync(user, password));
        }
        if (!await users.IsInRoleAsync(user, "Admin")) IdentityErrors.EnsureSuccess(await users.AddToRoleAsync(user, "Admin"));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
