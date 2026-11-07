using Management.Infrastructure.Authentication;
using Management.Infrastructure.Identity;
using Management.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Persistence;

public sealed class ManagementDbContext(DbContextOptions<ManagementDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int, IdentityUserClaim<int>,
        ApplicationUserRole, ApplicationUserLogin, IdentityRoleClaim<int>, ApplicationUserToken>(options)
{
    public DbSet<ProfileImage> ProfileImages => Set<ProfileImage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        });
        builder.Entity<ProfileImage>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.Url).HasMaxLength(300).IsRequired();
            b.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.Url).IsUnique();
            b.HasOne<ApplicationUser>().WithOne(user => user.ProfileImage)
                .HasForeignKey<ProfileImage>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ApplicationRole>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.HasIndex(x => x.IsBootstrapAdmin).IsUnique().HasFilter("[IsBootstrapAdmin] = 1");
        });
        builder.Entity<ApplicationUserRole>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        });
        builder.Entity<ApplicationUserLogin>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.LoginProvider).HasMaxLength(128);
            b.Property(x => x.ProviderKey).HasMaxLength(128);
            b.HasIndex(x => new { x.LoginProvider, x.ProviderKey }).IsUnique();
        });
        builder.Entity<ApplicationUserToken>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.LoginProvider).HasMaxLength(128);
            b.Property(x => x.Name).HasMaxLength(128);
            b.HasIndex(x => new { x.UserId, x.LoginProvider, x.Name }).IsUnique();
            b.HasIndex(x => x.SessionId);
            b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("IX_AspNetUserTokens_RefreshHash")
                .HasFilter($"[LoginProvider] = '{IdentityTokenProviders.Refresh}'");
        });
    }
}
