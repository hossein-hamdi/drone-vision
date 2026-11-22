using System.ComponentModel.DataAnnotations;

namespace Checker.Infrastructure.Authentication;

public sealed class JwtOptions
{
    [Required]
    public string Issuer { get; init; } = "DroneVision";

    [Required]
    public string Audience { get; init; } = "DroneVision.Client";

    [Required]
    public string SigningKey { get; init; } = string.Empty;

    public static bool HasStrongKey(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(options.SigningKey).Length >= 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
