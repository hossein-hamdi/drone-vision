using Management.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Management.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<int>, ITimestampedEntity
{
    public bool IsBootstrapAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class ApplicationUserRole : IdentityUserRole<int>, ITimestampedEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class ApplicationUserLogin : IdentityUserLogin<int>, ITimestampedEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class ApplicationUserClaim : IdentityUserClaim<int>, ITimestampedEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class ApplicationRoleClaim : IdentityRoleClaim<int>, ITimestampedEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class ApplicationUserToken : IdentityUserToken<int>, ITimestampedEntity
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public bool? IsBrowserSession { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
