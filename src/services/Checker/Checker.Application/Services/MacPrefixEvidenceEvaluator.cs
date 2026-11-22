using Checker.Application.Abstractions;

namespace Checker.Application.Services;

public sealed class MacPrefixEvidenceEvaluator(IDroneSignatureCatalog catalog) : INetworkEvidenceEvaluator
{
    public void Evaluate(EvidenceEvaluationContext context, EvidenceAccumulator accumulator)
    {
        var entry = catalog.FindBestPrefixMatch(context.MacAddress.CompactHex, out var matchedCompactPrefix);
        if (entry is not null)
        {
            accumulator.PrefixMatch = entry;
            accumulator.MatchedCompactPrefix = matchedCompactPrefix;
        }
    }
}
