using Checker.Domain.Enums;

namespace Checker.Domain.Entities;

public sealed record FrequencyProfile
{
    public required IReadOnlyList<FrequencyBand> Bands { get; init; }
    public required IReadOnlyList<string> FrequencyRangesMhz { get; init; }
    public string? ChannelSummary { get; init; }
}
