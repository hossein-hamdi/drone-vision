using Management.Application.Abstractions;
using Management.Application.Abstractions.Authentication;
using Management.Infrastructure.Authentication;
using Management.Infrastructure.Authorization;
using Management.Infrastructure.Identity;
using Management.Infrastructure.Persistence;
using Management.Infrastructure.Persistence.Seed;
using Management.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ManagementDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength = 12;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireNonAlphanumeric = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<ApplicationRole>().AddSignInManager().AddEntityFrameworkStores<ManagementDbContext>()
            .AddUserStore<ApplicationUserStore>();
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt").ValidateDataAnnotations()
            .Validate(JwtOptions.HasStrongKey, "Jwt:SigningKey must be a Base64-encoded key of at least 64 random bytes.").ValidateOnStart();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthenticationService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IProfileImageService, ProfileImageService>();
        services.AddSingleton<ProfileImageStore>();
        services.AddScoped<IdentityBootstrap>();
        return services;
    }
}
