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
    string? InterfaceName = null);

public sealed record WiFiScanBatch(
    IReadOnlyList<string> ScannedInterfaces,
    IReadOnlyList<ScannedWiFiNetwork> Networks);

public sealed record ScannedDroneNetworkDto
{
    public required string Bssid { get; init; }
    public string? Ssid { get; init; }
    public required bool IsDroneStation { get; init; }
    public required DetectionVerdict Verdict { get; init; }
    public required int ConfidenceScore { get; init; }
    public string? BrandMark { get; init; }
    public required IReadOnlyList<string> CandidateModels { get; init; }
    public required IReadOnlyList<string> FrequencyBands { get; init; }
    public required StationRole StationRole { get; init; }
    public int? SignalStrengthPercent { get; init; }
    public int? RssiDbm { get; init; }
    public string? Band { get; init; }
    public int? FrequencyMhz { get; init; }
    public int? Channel { get; init; }
    public IReadOnlyList<string> InterfaceNames { get; init; } = [];
}

public sealed record ScannedDroneNetworksResponse
{
    public required DateTimeOffset ScannedAtUtc { get; init; }
    public IReadOnlyList<string> ScannedInterfaces { get; init; } = [];
    public required int TotalNetworksScanned { get; init; }
    public required int DroneCount { get; init; }
    public required IReadOnlyList<ScannedDroneNetworkDto> Drones { get; init; }
}
