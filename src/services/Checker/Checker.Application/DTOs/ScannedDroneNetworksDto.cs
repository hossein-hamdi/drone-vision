using Checker.Domain.Enums;

namespace Checker.Application.DTOs;

public sealed record ScannedWiFiNetwork(
    string Bssid,
    string? Ssid,
    int? SignalStrengthPercent = null,
    int? RssiDbm = null,
    string? Band = null,
    int? FrequencyMhz = null,
    int? Channel = null,
    string? RadioType = null,
    string? Authentication = null);

public sealed record ScannedDroneNetworkDto
{
    public string? Ssid { get; init; }
    public required string Bssid { get; init; }
    public int? SignalStrengthPercent { get; init; }
    public int? RssiDbm { get; init; }
    public string? Band { get; init; }
    public int? FrequencyMhz { get; init; }
    public int? Channel { get; init; }
    public string? RadioType { get; init; }
    public string? Authentication { get; init; }
    public required bool IsDroneStation { get; init; }
    public required DetectionVerdict Verdict { get; init; }
    public required int ConfidenceScore { get; init; }
    public required MatchSource MatchSource { get; init; }
    public string? BrandMark { get; init; }
    public string? HardwareVendor { get; init; }
    public ChipsetExclusivity? ChipsetExclusivity { get; init; }
    public required StationRole StationRole { get; init; }
    public required IReadOnlyList<string> CandidateModels { get; init; }
    public required IReadOnlyList<string> SupportedFrequencyBands { get; init; }
    public required IReadOnlyList<string> FrequencyRangesMhz { get; init; }
    public required IReadOnlyList<string> RadioProtocols { get; init; }
    public required SignalRangeTier SignalRangeTier { get; init; }
    public string? MatchedPrefix { get; init; }
    public string? MatchedSsidPattern { get; init; }
    public required bool IsLocallyAdministered { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }
}

public sealed record ScannedDroneNetworksResponse
{
    public required DateTimeOffset ScannedAtUtc { get; init; }
    public required int TotalNetworksScanned { get; init; }
    public required int DroneCount { get; init; }
    public required IReadOnlyList<ScannedDroneNetworkDto> Drones { get; init; }
}
