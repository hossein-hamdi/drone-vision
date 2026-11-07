using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Management.Infrastructure.Authentication;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Value, int ExpiresIn) CreateAccessToken(int userId, int sessionId)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresIn = checked(settings.AccessTokenMinutes * 60);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim("sid", sessionId.ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)))
        ], now, now.AddSeconds(expiresIn),
            new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey)),
                SecurityAlgorithms.HmacSha512));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresIn);
    }

    public static string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
