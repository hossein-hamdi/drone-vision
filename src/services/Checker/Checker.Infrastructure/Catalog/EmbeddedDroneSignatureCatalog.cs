using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Checker.Application.Abstractions;
using Checker.Domain.Entities;

namespace Checker.Infrastructure.Catalog;

public sealed class EmbeddedDroneSignatureCatalog : IDroneSignatureCatalog
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FrozenDictionary<string, DroneSignatureEntry> _prefixes36;
    private readonly FrozenDictionary<string, DroneSignatureEntry> _prefixes28;
    private readonly FrozenDictionary<string, DroneSignatureEntry> _prefixes24;
    private readonly IReadOnlyList<DroneSignatureEntry> _allPrefixEntries;
    private readonly IReadOnlyList<(SsidPatternRule Rule, Regex Regex)> _ssidRules;

    public EmbeddedDroneSignatureCatalog()
    {
        var document = LoadEmbeddedCatalog();

        var p36 = new Dictionary<string, DroneSignatureEntry>(StringComparer.Ordinal);
        var p28 = new Dictionary<string, DroneSignatureEntry>(StringComparer.Ordinal);
        var p24 = new Dictionary<string, DroneSignatureEntry>(StringComparer.Ordinal);

        foreach (var entry in document.PrefixEntries)
        {
            foreach (var rawPrefix in entry.Prefixes)
            {
                var compact = NormalizePrefix(rawPrefix);
                switch (compact.Length)
                {
                    case 9:
                        p36[compact] = entry;
                        break;
                    case 7:
                        p28[compact] = entry;
                        break;
                    case 6:
                        p24[compact] = entry;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Invalid prefix '{rawPrefix}' in signature '{entry.Id}': expected 6, 7, or 9 hex chars.");
                }
            }
        }

        _prefixes36 = p36.ToFrozenDictionary(StringComparer.Ordinal);
        _prefixes28 = p28.ToFrozenDictionary(StringComparer.Ordinal);
        _prefixes24 = p24.ToFrozenDictionary(StringComparer.Ordinal);
        _allPrefixEntries = document.PrefixEntries.AsReadOnly();

        _ssidRules = document.SsidRules
            .Select(rule => (
                Rule: rule,
                Regex: new Regex(
                    rule.Pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
                    RegexTimeout)))
            .ToArray();
    }

    public DroneSignatureEntry? FindBestPrefixMatch(string compactHex, out string? matchedCompactPrefix)
    {
        if (compactHex.Length == 12)
        {
            var key36 = compactHex[..9];
            if (_prefixes36.TryGetValue(key36, out var match36))
            {
                matchedCompactPrefix = key36;
                return match36;
            }

            var key28 = compactHex[..7];
            if (_prefixes28.TryGetValue(key28, out var match28))
            {
                matchedCompactPrefix = key28;
                return match28;
            }

            var key24 = compactHex[..6];
            if (_prefixes24.TryGetValue(key24, out var match24))
            {
                matchedCompactPrefix = key24;
                return match24;
            }
        }

        matchedCompactPrefix = null;
        return null;
    }

    public IReadOnlyList<SsidPatternRule> FindMatchingSsidRules(string ssid)
    {
        if (string.IsNullOrEmpty(ssid))
        {
            return [];
        }

        var matches = new List<SsidPatternRule>();
        foreach (var (rule, regex) in _ssidRules)
        {
            try
            {
                if (regex.IsMatch(ssid))
                {
                    matches.Add(rule);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Bounded evaluation protects against unexpected input timeouts.
            }
        }

        return matches;
    }

    public IReadOnlyList<DroneSignatureEntry> GetAllPrefixEntries() => _allPrefixEntries;

    public IReadOnlyList<SsidPatternRule> GetAllSsidRules() =>
        _ssidRules.Select(static x => x.Rule).ToArray();

    private static string NormalizePrefix(string rawPrefix)
    {
        var chars = rawPrefix.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray();
        return new string(chars);
    }

    private static CatalogDocument LoadEmbeddedCatalog()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceSuffix = "drone-signatures.json";
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded signature catalog '{resourceSuffix}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Unable to open embedded resource '{resourceName}'.");

        return JsonSerializer.Deserialize<CatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize drone signature catalog.");
    }

    private sealed class CatalogDocument
    {
        public List<DroneSignatureEntry> PrefixEntries { get; init; } = [];
        public List<SsidPatternRule> SsidRules { get; init; } = [];
    }
}
