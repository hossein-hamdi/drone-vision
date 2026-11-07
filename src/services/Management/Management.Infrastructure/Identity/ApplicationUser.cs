using Management.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;

namespace Management.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = "";
    public ProfileImage? ProfileImage { get; set; }
}
