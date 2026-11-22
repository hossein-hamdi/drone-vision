using Checker.Domain.Entities;

namespace Checker.Application.Abstractions;

public interface IDroneSignatureCatalog
{
    DroneSignatureEntry? FindBestPrefixMatch(string compactHex, out string? matchedCompactPrefix);
    IReadOnlyList<SsidPatternRule> FindMatchingSsidRules(string ssid);
    IReadOnlyList<DroneSignatureEntry> GetAllPrefixEntries();
    IReadOnlyList<SsidPatternRule> GetAllSsidRules();
}
