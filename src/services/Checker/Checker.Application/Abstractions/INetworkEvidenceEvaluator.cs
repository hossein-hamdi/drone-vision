using Checker.Domain.Entities;
using Checker.Domain.ValueObjects;

namespace Checker.Application.Abstractions;

public sealed record EvidenceEvaluationContext(MacAddress MacAddress, SsidName? Ssid);

public sealed record NetworkEvidenceResult(
    DroneSignatureEntry? PrefixMatch,
    string? MatchedCompactPrefix,
    IReadOnlyList<SsidPatternRule> MatchedSsidRules);

public interface INetworkEvidenceEvaluator
{
    void Evaluate(EvidenceEvaluationContext context, EvidenceAccumulator accumulator);
}

public sealed class EvidenceAccumulator
{
    public DroneSignatureEntry? PrefixMatch { get; set; }
    public string? MatchedCompactPrefix { get; set; }
    public List<SsidPatternRule> MatchedSsidRules { get; } = [];

    public NetworkEvidenceResult ToResult() =>
        new(PrefixMatch, MatchedCompactPrefix, MatchedSsidRules.AsReadOnly());
}
