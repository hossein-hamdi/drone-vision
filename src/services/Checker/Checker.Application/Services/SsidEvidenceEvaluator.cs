using Checker.Application.Abstractions;

namespace Checker.Application.Services;

public sealed class SsidEvidenceEvaluator(IDroneSignatureCatalog catalog) : INetworkEvidenceEvaluator
{
    public void Evaluate(EvidenceEvaluationContext context, EvidenceAccumulator accumulator)
    {
        if (context.Ssid is null)
        {
            return;
        }

        var matches = catalog.FindMatchingSsidRules(context.Ssid.Value);
        if (matches.Count > 0)
        {
            accumulator.MatchedSsidRules.AddRange(matches);
        }
    }
}
