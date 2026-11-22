using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Checker.Tests.Integration;

public sealed class CheckerApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultIssuer = "DroneVision";
    public const string DefaultAudience = "DroneVision.Client";

    private readonly string _environmentName;
    private readonly string _signingKeyBase64;
    private readonly int _rateLimitPerMinute;
    private readonly string? _gatewayPathBase;

    public CheckerApiFactory()
        : this("Development", null, 60, null)
    {
    }

    internal CheckerApiFactory(
        string environmentName = "Development",
        string? signingKeyBase64 = null,
        int rateLimitPerMinute = 60,
        string? gatewayPathBase = null)
    {
        _environmentName = environmentName;
        _signingKeyBase64 = signingKeyBase64 ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _rateLimitPerMinute = rateLimitPerMinute;
        _gatewayPathBase = gatewayPathBase;
    }

    public string SigningKeyBase64 => _signingKeyBase64;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = DefaultIssuer,
                ["Jwt:Audience"] = DefaultAudience,
                ["Jwt:SigningKey"] = _signingKeyBase64,
                ["RateLimiting:LookupRequestsPerMinute"] = _rateLimitPerMinute.ToString(),
                ["Gateway:PathBase"] = _gatewayPathBase
            };
            config.AddInMemoryCollection(overrides);
        });
    }

    public string CreateToken(
        string? issuer = DefaultIssuer,
        string? audience = DefaultAudience,
        string? signingKeyBase64 = null,
        string algorithm = SecurityAlgorithms.HmacSha512,
        DateTime? expiresUtc = null,
        string? sub = "42",
        string? sid = "7",
        string? jti = "token-id-123")
    {
        var keyBytes = Convert.FromBase64String(signingKeyBase64 ?? _signingKeyBase64);
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, algorithm);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>();
        if (sub is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));
        }

        if (sid is not null)
        {
            claims.Add(new Claim("sid", sid));
        }

        if (jti is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: expiresUtc ?? now.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
