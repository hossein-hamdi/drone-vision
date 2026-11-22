using System.ComponentModel.DataAnnotations;

namespace Checker.Infrastructure.Authentication;

public sealed class ManagementSessionOptions
{
    public string? BaseUrl { get; init; }

    [Range(1, 30)]
    public int TimeoutSeconds { get; init; } = 3;
}
