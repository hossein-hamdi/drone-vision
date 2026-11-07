using Microsoft.AspNetCore.Identity;

namespace Management.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<int>
{
    public bool IsBootstrapAdmin { get; set; }
}
public sealed class ApplicationUserRole : IdentityUserRole<int> { public int Id { get; set; } }
public sealed class ApplicationUserLogin : IdentityUserLogin<int> { public int Id { get; set; } }
public sealed class ApplicationUserToken : IdentityUserToken<int>
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public bool? IsBrowserSession { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
