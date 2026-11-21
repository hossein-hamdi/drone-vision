using Management.Infrastructure.Authentication;
using Management.Infrastructure.Identity;
using Management.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Management.Infrastructure.Persistence;

public sealed class ManagementDbContext(DbContextOptions<ManagementDbContext> options, TimeProvider? clock = null)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int, ApplicationUserClaim,
        ApplicationUserRole, ApplicationUserLogin, ApplicationRoleClaim, ApplicationUserToken>(options)
{
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public DbSet<ProfileImage> ProfileImages => Set<ProfileImage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<ITimestampedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(e => typeof(ITimestampedEntity).IsAssignableFrom(e.ClrType)))
        {
            var entity = builder.Entity(entityType.ClrType);
            entity.Property(nameof(ITimestampedEntity.CreatedAt))
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .HasConversion(UtcDateTimeConverter);
            entity.Property(nameof(ITimestampedEntity.UpdatedAt))
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .HasConversion(UtcDateTimeConverter);
        }
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
