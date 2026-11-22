using Checker.Domain.Enums;

namespace Checker.Domain.Entities;

public sealed record SsidPatternRule
{
    public required string Id { get; init; }
    public required string BrandMark { get; init; }
    public required string Pattern { get; init; }
    public required string DisplayPattern { get; init; }
    public required IReadOnlyList<string> CandidateModels { get; init; }
    public required StationRole StationRole { get; init; }
    public required FrequencyProfile FrequencyProfile { get; init; }
    public required IReadOnlyList<string> RadioProtocols { get; init; }
    public required SignalRangeTier SignalRangeTier { get; init; }
    public required int ConfidenceWeight { get; init; }
    public IReadOnlyList<string>? AssociatedChipsetVendors { get; init; }
    public string? Notes { get; init; }
}
