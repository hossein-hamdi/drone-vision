using System.ComponentModel.DataAnnotations;

namespace Management.Infrastructure.Authentication;

public sealed class JwtOptions
{
    [Required] public string Issuer { get; init; } = "DroneVision";
    [Required] public string Audience { get; init; } = "DroneVision.Client";
    [Required] public string SigningKey { get; init; } = "";
    [Range(1, 60)] public int AccessTokenMinutes { get; init; } = 15;
    [Range(1, 30)] public int RefreshTokenDays { get; init; } = 7;

    public static bool HasStrongKey(JwtOptions options)
    {
        try { return Convert.FromBase64String(options.SigningKey).Length >= 64; }
        catch (FormatException) { return false; }
    }
}
