using Management.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Identity;

// The built-in store assumes composite primary keys for these three records.
// Query their unique business keys explicitly while retaining integer identity primary keys.
public sealed class ApplicationUserStore(ManagementDbContext db, IdentityErrorDescriber describer)
    : UserStore<ApplicationUser, ApplicationRole, ManagementDbContext, int, IdentityUserClaim<int>,
        ApplicationUserRole, ApplicationUserLogin, ApplicationUserToken, IdentityRoleClaim<int>>(db, describer)
{
    protected override Task<ApplicationUserRole?> FindUserRoleAsync(int userId, int roleId, CancellationToken ct) =>
        Context.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, ct);

    protected override Task<ApplicationUserLogin?> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken ct) =>
        Context.UserLogins.SingleOrDefaultAsync(x => x.LoginProvider == loginProvider && x.ProviderKey == providerKey, ct);

    protected override Task<ApplicationUserLogin?> FindUserLoginAsync(int userId, string loginProvider, string providerKey, CancellationToken ct) =>
        Context.UserLogins.SingleOrDefaultAsync(x => x.UserId == userId && x.LoginProvider == loginProvider && x.ProviderKey == providerKey, ct);

    protected override Task<ApplicationUserToken?> FindTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken ct) =>
        Context.UserTokens.SingleOrDefaultAsync(x => x.UserId == user.Id && x.LoginProvider == loginProvider && x.Name == name, ct);
}
