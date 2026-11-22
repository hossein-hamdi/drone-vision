using Checker.Application.Abstractions;
using Checker.Application.Services;
using Checker.Infrastructure.Authentication;
using Checker.Infrastructure.Catalog;
using Checker.Infrastructure.Scanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Checker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCheckerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .Validate(
                JwtOptions.HasStrongKey,
                "Jwt:SigningKey must be a Base64-encoded key of at least 64 random bytes.")
            .ValidateOnStart();

        services.AddOptions<ManagementSessionOptions>()
            .Bind(configuration.GetSection("Management"))
            .ValidateDataAnnotations()
            .Validate(
                static o => string.IsNullOrWhiteSpace(o.BaseUrl) ||
                            (Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) &&
                             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)),
                "Management:BaseUrl must be an absolute HTTP or HTTPS URI when configured.")
            .ValidateOnStart();

        services.AddSingleton<IDroneSignatureCatalog, EmbeddedDroneSignatureCatalog>();
        services.AddSingleton<INetworkEvidenceEvaluator, MacPrefixEvidenceEvaluator>();
        services.AddSingleton<INetworkEvidenceEvaluator, SsidEvidenceEvaluator>();
        services.AddSingleton<VerdictScoringPolicy>();
        services.AddScoped<IWiFiScanner, PlatformWiFiScanner>();
        services.AddScoped<IDroneNetworkCheckerService, DroneNetworkCheckerService>();

        return services;
    }
}
