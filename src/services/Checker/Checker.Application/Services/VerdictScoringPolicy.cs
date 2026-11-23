using Checker.Application.Abstractions;
using Checker.Application.DTOs;
using Checker.Domain.Entities;
using Checker.Domain.Enums;
using Checker.Domain.ValueObjects;

namespace Checker.Application.Services;

public sealed class VerdictScoringPolicy
{
    public DroneNetworkEvaluation EvaluateVerdict(
        MacAddress mac,
        SsidName? ssid,
        NetworkEvidenceResult evidence)
    {
        var prefix = evidence.PrefixMatch;
        var matchedPrefixFormatted = evidence.MatchedCompactPrefix is not null
            ? MacAddress.FormatPrefix(evidence.MatchedCompactPrefix)
            : null;
        var bestSsidRule = evidence.MatchedSsidRules
            .OrderByDescending(static r => r.ConfidenceWeight)
            .FirstOrDefault();

        var notes = new List<string>();
        if (mac.IsLocallyAdministered)
        {
            notes.Add("BSSID has the locally administered address (LAA) bit set; MAC address may be randomized and cannot reliably attribute vendor hardware on its own.");
        }

        if (mac.IsMulticast)
        {
            notes.Add("BSSID has the IEEE multicast/group bit set, which is atypical for a standard access point BSSID.");
        }

        // Case 1: LAA bit set -> never confirm via OUI alone; attribute via SSID if matched.
        if (mac.IsLocallyAdministered)
        {
            return BuildLaaResponse(mac, ssid, matchedPrefixFormatted, prefix, bestSsidRule, notes);
        }

        // Case 2: Both OUI/MAC prefix and SSID matched.
        if (prefix is not null && bestSsidRule is not null)
        {
            var sameEcosystem = string.Equals(prefix.BrandMark, bestSsidRule.BrandMark, StringComparison.OrdinalIgnoreCase)
                || prefix.Exclusivity == ChipsetExclusivity.SharedConsumerChipset
                || (bestSsidRule.AssociatedChipsetVendors?.Any(v =>
                    prefix.VendorName.Contains(v, StringComparison.OrdinalIgnoreCase)) ?? false);

            var score = prefix.Exclusivity == ChipsetExclusivity.DedicatedDroneVendor
                ? (sameEcosystem ? Math.Clamp(Math.Max(95, bestSsidRule.ConfidenceWeight + 5), 95, 100) : 92)
                : Math.Clamp(Math.Max(85, bestSsidRule.ConfidenceWeight), 85, 95);

            notes.Add(prefix.Exclusivity == ChipsetExclusivity.DedicatedDroneVendor
                ? $"Correlated dedicated drone manufacturer prefix ({prefix.BrandMark}) with matching SSID pattern '{bestSsidRule.DisplayPattern}'."
                : $"Correlated shared consumer Wi-Fi chipset ({prefix.VendorName}) with drone SSID pattern '{bestSsidRule.DisplayPattern}'.");

            AddNotes(notes, prefix.Notes, bestSsidRule.Notes);

            return CreateResponse(
                isDroneStation: true,
                verdict: DetectionVerdict.Confirmed,
                confidenceScore: score,
                matchSource: MatchSource.OuiAndSsid,
                mac: mac,
                matchedPrefix: matchedPrefixFormatted,
                ssid: ssid,
                matchedSsidPattern: bestSsidRule.DisplayPattern,
                hardwareVendor: prefix.VendorName,
                brandMark: bestSsidRule.BrandMark,
                chipsetExclusivity: prefix.Exclusivity,
                stationRole: bestSsidRule.StationRole != StationRole.Unknown ? bestSsidRule.StationRole : prefix.StationRole,
                candidateModels: MergeDistinct(bestSsidRule.CandidateModels, sameEcosystem ? prefix.CandidateModels : []),
                frequencyProfile: bestSsidRule.FrequencyProfile,
                radioProtocols: MergeDistinct(bestSsidRule.RadioProtocols, prefix.RadioProtocols),
                signalRangeTier: bestSsidRule.SignalRangeTier != SignalRangeTier.Unknown ? bestSsidRule.SignalRangeTier : prefix.SignalRangeTier,
                knownSsidPatterns: MergeDistinct([bestSsidRule.DisplayPattern], prefix.AssociatedSsidPatterns),
                notes: notes);
        }

        // Case 3: Dedicated drone OUI matched (with no matching SSID rule).
        if (prefix is { Exclusivity: ChipsetExclusivity.DedicatedDroneVendor })
        {
            var ssidProvidedUnmatched = ssid is not null;
            var verdict = ssidProvidedUnmatched ? DetectionVerdict.Probable : DetectionVerdict.Confirmed;
            var score = ssidProvidedUnmatched ? 76 : 90;

            notes.Add(ssidProvidedUnmatched
                ? $"Matched dedicated drone manufacturer IEEE block ({prefix.BrandMark}), though supplied SSID '{ssid!.Value}' did not match default brand naming conventions (may be custom-renamed)."
                : $"Matched dedicated drone manufacturer IEEE block ({prefix.BrandMark}).");
            AddNotes(notes, prefix.Notes);

            return CreateResponse(
                isDroneStation: true,
                verdict: verdict,
                confidenceScore: score,
                matchSource: MatchSource.Oui,
                mac: mac,
                matchedPrefix: matchedPrefixFormatted,
                ssid: ssid,
                matchedSsidPattern: null,
                hardwareVendor: prefix.VendorName,
                brandMark: prefix.BrandMark,
                chipsetExclusivity: prefix.Exclusivity,
                stationRole: prefix.StationRole,
                candidateModels: prefix.CandidateModels,
                frequencyProfile: prefix.FrequencyProfile,
                radioProtocols: prefix.RadioProtocols,
                signalRangeTier: prefix.SignalRangeTier,
                knownSsidPatterns: prefix.AssociatedSsidPatterns,
                notes: notes);
        }

        // Case 4: Shared consumer Wi-Fi chipset matched WITHOUT a drone SSID -> conservative non-confirmation.
        if (prefix is { Exclusivity: ChipsetExclusivity.SharedConsumerChipset })
        {
            notes.Add($"Matched shared consumer Wi-Fi chipset ({prefix.VendorName}) commonly used in both general-purpose IoT hardware and budget Wi-Fi drones; supply a corroborating SSID to confirm.");
            AddNotes(notes, prefix.Notes);

            return CreateResponse(
                isDroneStation: false,
                verdict: DetectionVerdict.Possible,
                confidenceScore: 30,
                matchSource: MatchSource.Oui,
                mac: mac,
                matchedPrefix: matchedPrefixFormatted,
                ssid: ssid,
                matchedSsidPattern: null,
                hardwareVendor: prefix.VendorName,
                brandMark: prefix.BrandMark,
                chipsetExclusivity: prefix.Exclusivity,
                stationRole: prefix.StationRole,
                candidateModels: prefix.CandidateModels,
                frequencyProfile: prefix.FrequencyProfile,
                radioProtocols: prefix.RadioProtocols,
                signalRangeTier: prefix.SignalRangeTier,
                knownSsidPatterns: prefix.AssociatedSsidPatterns,
                notes: notes);
        }

        // Case 5: No known MAC prefix, but SSID matched a known drone pattern.
        if (bestSsidRule is not null)
        {
            var score = Math.Clamp(bestSsidRule.ConfidenceWeight, 75, 88);
            notes.Add($"Matched drone SSID pattern '{bestSsidRule.DisplayPattern}' ({bestSsidRule.BrandMark}); BSSID prefix is not registered as a dedicated drone vendor.");
            AddNotes(notes, bestSsidRule.Notes);

            return CreateResponse(
                isDroneStation: true,
                verdict: DetectionVerdict.Probable,
                confidenceScore: score,
                matchSource: MatchSource.Ssid,
                mac: mac,
                matchedPrefix: null,
                ssid: ssid,
                matchedSsidPattern: bestSsidRule.DisplayPattern,
                hardwareVendor: null,
                brandMark: bestSsidRule.BrandMark,
                chipsetExclusivity: null,
                stationRole: bestSsidRule.StationRole,
                candidateModels: bestSsidRule.CandidateModels,
                frequencyProfile: bestSsidRule.FrequencyProfile,
                radioProtocols: bestSsidRule.RadioProtocols,
                signalRangeTier: bestSsidRule.SignalRangeTier,
                knownSsidPatterns: [bestSsidRule.DisplayPattern],
                notes: notes);
        }

        // Case 6: Not recognized.
        notes.Add("BSSID and optional SSID did not match any known drone manufacturer OUI, shared drone chipset, or drone SSID pattern.");

        return CreateResponse(
            isDroneStation: false,
            verdict: DetectionVerdict.NotRecognized,
            confidenceScore: 0,
            matchSource: MatchSource.None,
            mac: mac,
            matchedPrefix: null,
            ssid: ssid,
            matchedSsidPattern: null,
            hardwareVendor: null,
            brandMark: null,
            chipsetExclusivity: null,
            stationRole: StationRole.Unknown,
            candidateModels: [],
            frequencyProfile: null,
            radioProtocols: [],
            signalRangeTier: SignalRangeTier.Unknown,
            knownSsidPatterns: [],
            notes: notes);
    }

    private static DroneNetworkEvaluation BuildLaaResponse(
        MacAddress mac,
        SsidName? ssid,
        string? matchedPrefixFormatted,
        DroneSignatureEntry? prefix,
        SsidPatternRule? bestSsidRule,
        List<string> notes)
    {
        if (bestSsidRule is not null)
        {
            var score = Math.Clamp(bestSsidRule.ConfidenceWeight - 15, 65, 78);
            notes.Add($"Attributed via SSID pattern '{bestSsidRule.DisplayPattern}' despite randomized/locally administered BSSID.");
            AddNotes(notes, bestSsidRule.Notes);

            return CreateResponse(
                isDroneStation: true,
                verdict: DetectionVerdict.Probable,
                confidenceScore: score,
                matchSource: MatchSource.Ssid,
                mac: mac,
                matchedPrefix: matchedPrefixFormatted,
                ssid: ssid,
                matchedSsidPattern: bestSsidRule.DisplayPattern,
                hardwareVendor: prefix?.VendorName,
                brandMark: bestSsidRule.BrandMark,
                chipsetExclusivity: prefix?.Exclusivity,
                stationRole: bestSsidRule.StationRole,
                candidateModels: bestSsidRule.CandidateModels,
                frequencyProfile: bestSsidRule.FrequencyProfile,
                radioProtocols: bestSsidRule.RadioProtocols,
                signalRangeTier: bestSsidRule.SignalRangeTier,
                knownSsidPatterns: [bestSsidRule.DisplayPattern],
                notes: notes);
        }

        notes.Add("No drone SSID provided or matched; BSSID cannot be attributed reliably when locally administered.");

        return CreateResponse(
            isDroneStation: false,
            verdict: DetectionVerdict.NotRecognized,
            confidenceScore: 0,
            matchSource: MatchSource.None,
            mac: mac,
            matchedPrefix: null,
            ssid: ssid,
            matchedSsidPattern: null,
            hardwareVendor: null,
            brandMark: null,
            chipsetExclusivity: null,
            stationRole: StationRole.Unknown,
            candidateModels: [],
            frequencyProfile: null,
            radioProtocols: [],
            signalRangeTier: SignalRangeTier.Unknown,
            knownSsidPatterns: [],
            notes: notes);
    }

    private static DroneNetworkEvaluation CreateResponse(
        bool isDroneStation,
        DetectionVerdict verdict,
        int confidenceScore,
        MatchSource matchSource,
        MacAddress mac,
        string? matchedPrefix,
        SsidName? ssid,
        string? matchedSsidPattern,
        string? hardwareVendor,
        string? brandMark,
        ChipsetExclusivity? chipsetExclusivity,
        StationRole stationRole,
        IReadOnlyList<string> candidateModels,
        FrequencyProfile? frequencyProfile,
        IReadOnlyList<string> radioProtocols,
        SignalRangeTier signalRangeTier,
        IReadOnlyList<string> knownSsidPatterns,
        List<string> notes) => new()
        {
            IsDroneStation = isDroneStation,
            Verdict = verdict,
            ConfidenceScore = confidenceScore,
            MatchSource = matchSource,
            NormalizedBssid = mac.Canonical,
            OuiPrefix = mac.Oui24,
            MatchedPrefix = matchedPrefix,
            IsLocallyAdministered = mac.IsLocallyAdministered,
            IsMulticast = mac.IsMulticast,
            ObservedSsid = ssid?.Value,
            MatchedSsidPattern = matchedSsidPattern,
            HardwareVendor = hardwareVendor,
            BrandMark = brandMark,
            ChipsetExclusivity = chipsetExclusivity,
            StationRole = stationRole,
            CandidateModels = candidateModels,
            FrequencyBands = frequencyProfile?.Bands.Select(static b => b.ToDisplayLabel()).ToArray() ?? [],
            FrequencyRangesMhz = frequencyProfile?.FrequencyRangesMhz ?? [],
            ChannelSummary = frequencyProfile?.ChannelSummary,
            RadioProtocols = radioProtocols,
            SignalRangeTier = signalRangeTier,
            KnownSsidPatterns = knownSsidPatterns,
            Notes = notes
        };

    private static IReadOnlyList<string> MergeDistinct(IReadOnlyList<string> primary, IReadOnlyList<string> secondary) =>
        primary.Concat(secondary).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void AddNotes(List<string> notes, params string?[] candidates)
    {
        foreach (var note in candidates)
        {
            if (!string.IsNullOrWhiteSpace(note) && !notes.Contains(note, StringComparer.Ordinal))
            {
                notes.Add(note);
            }
        }
    }
}
