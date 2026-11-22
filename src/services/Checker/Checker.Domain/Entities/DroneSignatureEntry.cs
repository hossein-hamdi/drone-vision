using Checker.Domain.Enums;

namespace Checker.Domain.Entities;

public sealed record DroneSignatureEntry
{
    public required string Id { get; init; }
    public required IReadOnlyList<string> Prefixes { get; init; }
    public required string VendorName { get; init; }
    public required string BrandMark { get; init; }
    public required ChipsetExclusivity Exclusivity { get; init; }
    public required IReadOnlyList<string> CandidateModels { get; init; }
    public required StationRole StationRole { get; init; }
    public required FrequencyProfile FrequencyProfile { get; init; }
    public required IReadOnlyList<string> RadioProtocols { get; init; }
    public required SignalRangeTier SignalRangeTier { get; init; }
    public required IReadOnlyList<string> AssociatedSsidPatterns { get; init; }
    public string? Notes { get; init; }
}
