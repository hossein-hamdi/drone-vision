using Checker.Domain.Enums;

namespace Checker.Application.DTOs;

public sealed record DroneNetworkCheckResponse
{
    public required bool IsDroneStation { get; init; }
    public required DetectionVerdict Verdict { get; init; }
    public required int ConfidenceScore { get; init; }
    public required MatchSource MatchSource { get; init; }
    public required string NormalizedBssid { get; init; }
    public required string OuiPrefix { get; init; }
    public string? MatchedPrefix { get; init; }
    public required bool IsLocallyAdministered { get; init; }
    public required bool IsMulticast { get; init; }
    public string? ObservedSsid { get; init; }
    public string? MatchedSsidPattern { get; init; }
    public string? HardwareVendor { get; init; }
    public string? BrandMark { get; init; }
    public ChipsetExclusivity? ChipsetExclusivity { get; init; }
    public required StationRole StationRole { get; init; }
    public required IReadOnlyList<string> CandidateModels { get; init; }
    public required IReadOnlyList<string> FrequencyBands { get; init; }
    public required IReadOnlyList<string> FrequencyRangesMhz { get; init; }
    public string? ChannelSummary { get; init; }
    public required IReadOnlyList<string> RadioProtocols { get; init; }
    public required SignalRangeTier SignalRangeTier { get; init; }
    public required IReadOnlyList<string> KnownSsidPatterns { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }
}
