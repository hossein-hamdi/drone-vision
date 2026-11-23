using Checker.Domain.Enums;

namespace Checker.Application.DTOs;

public sealed record DroneNetworkCheckResponse
{
    public required bool IsDroneStation { get; init; }
    public required DetectionVerdict Verdict { get; init; }
    public required int ConfidenceScore { get; init; }
    public required string Bssid { get; init; }
    public string? Ssid { get; init; }
    public string? BrandMark { get; init; }
    public required IReadOnlyList<string> CandidateModels { get; init; }
    public required IReadOnlyList<string> FrequencyBands { get; init; }
    public required StationRole StationRole { get; init; }
}
