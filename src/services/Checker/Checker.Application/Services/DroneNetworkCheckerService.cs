using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Checker.Domain.Enums;
using Checker.Domain.ValueObjects;

namespace Checker.Application.Services;

public sealed class DroneNetworkCheckerService(
    IEnumerable<INetworkEvidenceEvaluator> evaluators,
    VerdictScoringPolicy scoringPolicy,
    IWiFiScanner wifiScanner,
    TimeProvider clock) : IDroneNetworkCheckerService
{
    public DroneNetworkCheckerService(
        MacPrefixEvidenceEvaluator macEvaluator,
        SsidEvidenceEvaluator ssidEvaluator,
        VerdictScoringPolicy scoringPolicy,
        IWiFiScanner? wifiScanner = null,
        TimeProvider? clock = null)
        : this(
            [macEvaluator, ssidEvaluator],
            scoringPolicy,
            wifiScanner ?? new UnavailableWiFiScanner(),
            clock ?? TimeProvider.System)
    {
    }

    public Task<DroneNetworkCheckResponse> CheckAsync(DroneNetworkCheckQuery query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!MacAddress.TryParse(query.Bssid, null, out var mac))
        {
            errors["bssid"] = ["A valid MAC/BSSID address (non-zero, non-broadcast) is required."];
        }

        SsidName? ssid = null;
        if (query.SsidWasProvided || query.Ssid is not null)
        {
            if (!SsidName.TryCreate(query.Ssid, out ssid, out var ssidError))
            {
                errors["ssid"] = [ssidError];
            }
        }

        if (errors.Count > 0 || mac is null)
        {
            throw ApiException.Validation(errors);
        }

        return Task.FromResult(EvaluateSingle(mac, ssid));
    }

    public async Task<ScannedDroneNetworksResponse> ScanDroneNetworksAsync(
        bool includePossible = false,
        CancellationToken ct = default)
    {
        var scanned = await wifiScanner.ScanAsync(ct);
        var matched = new List<ScannedDroneNetworkDto>();
        var validScannedCount = 0;

        foreach (var item in scanned)
        {
            ct.ThrowIfCancellationRequested();

            if (!MacAddress.TryParse(item.Bssid, null, out var mac))
            {
                continue;
            }

            validScannedCount++;

            SsidName? ssid = null;
            if (!string.IsNullOrWhiteSpace(item.Ssid))
            {
                SsidName.TryCreate(item.Ssid, out ssid, out _);
            }

            var evaluation = EvaluateSingle(mac, ssid);
            if (!evaluation.IsDroneStation && !(includePossible && evaluation.Verdict == DetectionVerdict.Possible))
            {
                continue;
            }

            matched.Add(new ScannedDroneNetworkDto
            {
                Ssid = evaluation.ObservedSsid ?? (string.IsNullOrWhiteSpace(item.Ssid) ? null : item.Ssid.Trim()),
                Bssid = evaluation.NormalizedBssid,
                SignalStrengthPercent = item.SignalStrengthPercent,
                RssiDbm = item.RssiDbm,
                Band = item.Band,
                FrequencyMhz = item.FrequencyMhz,
                Channel = item.Channel,
                RadioType = item.RadioType,
                Authentication = item.Authentication,
                IsDroneStation = evaluation.IsDroneStation,
                Verdict = evaluation.Verdict,
                ConfidenceScore = evaluation.ConfidenceScore,
                MatchSource = evaluation.MatchSource,
                BrandMark = evaluation.BrandMark,
                HardwareVendor = evaluation.HardwareVendor,
                ChipsetExclusivity = evaluation.ChipsetExclusivity,
                StationRole = evaluation.StationRole,
                CandidateModels = evaluation.CandidateModels,
                SupportedFrequencyBands = evaluation.FrequencyBands,
                FrequencyRangesMhz = evaluation.FrequencyRangesMhz,
                RadioProtocols = evaluation.RadioProtocols,
                SignalRangeTier = evaluation.SignalRangeTier,
                MatchedPrefix = evaluation.MatchedPrefix,
                MatchedSsidPattern = evaluation.MatchedSsidPattern,
                IsLocallyAdministered = evaluation.IsLocallyAdministered,
                Notes = evaluation.Notes
            });
        }

        var ordered = matched
            .OrderByDescending(static d => d.ConfidenceScore)
            .ThenByDescending(static d => d.SignalStrengthPercent ?? int.MinValue)
            .ThenBy(static d => d.Bssid, StringComparer.Ordinal)
            .ToArray();

        return new ScannedDroneNetworksResponse
        {
            ScannedAtUtc = clock.GetUtcNow(),
            TotalNetworksScanned = validScannedCount,
            DroneCount = ordered.Length,
            Drones = ordered
        };
    }

    private DroneNetworkCheckResponse EvaluateSingle(MacAddress mac, SsidName? ssid)
    {
        var context = new EvidenceEvaluationContext(mac, ssid);
        var accumulator = new EvidenceAccumulator();
        foreach (var evaluator in evaluators)
        {
            evaluator.Evaluate(context, accumulator);
        }

        return scoringPolicy.EvaluateVerdict(mac, ssid, accumulator.ToResult());
    }

    private sealed class UnavailableWiFiScanner : IWiFiScanner
    {
        public Task<IReadOnlyList<ScannedWiFiNetwork>> ScanAsync(CancellationToken ct = default) =>
            throw ApiException.ServiceUnavailable("No Wi-Fi scanner is configured.");
    }
}
