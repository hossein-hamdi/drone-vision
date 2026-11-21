using Management.Infrastructure.Persistence;
using Management.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;

namespace Management.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<int>, ITimestampedEntity
{
    public string FullName { get; set; } = "";
    public ProfileImage? ProfileImage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
