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

        var evaluation = EvaluateSingle(mac, ssid);
        return Task.FromResult(new DroneNetworkCheckResponse
        {
            IsDroneStation = evaluation.IsDroneStation,
            Verdict = evaluation.Verdict,
            ConfidenceScore = evaluation.ConfidenceScore,
            Bssid = evaluation.NormalizedBssid,
            Ssid = evaluation.ObservedSsid,
            BrandMark = evaluation.BrandMark,
            CandidateModels = evaluation.CandidateModels,
            FrequencyBands = evaluation.FrequencyBands,
            StationRole = evaluation.StationRole
        });
    }

    public async Task<WlanInterfacesResponse> GetWlanInterfacesAsync(CancellationToken ct = default)
    {
        var discovery = await wifiScanner.DiscoverInterfacesAsync(ct);
        return new WlanInterfacesResponse
        {
            Available = discovery.Available,
            Reason = discovery.Reason,
            Interfaces = discovery.Interfaces
                .Select(static i => new WlanInterfaceDto
                {
                    Name = i.Name,
                    State = i.State,
                    IsUsableForScan = i.IsUsableForScan,
                    Description = i.Description,
                    MacAddress = i.MacAddress
                })
                .ToArray()
        };
    }

    public async Task<ScannedDroneNetworksResponse> ScanDroneNetworksAsync(
        bool includePossible = false,
        CancellationToken ct = default)
    {
        var batch = await wifiScanner.ScanAsync(ct);
        var groupedByBssid = new Dictionary<string, (MacAddress Mac, List<ScannedWiFiNetwork> Observations)>(StringComparer.Ordinal);

        foreach (var item in batch.Networks)
        {
            ct.ThrowIfCancellationRequested();

            if (!MacAddress.TryParse(item.Bssid, null, out var mac))
            {
                continue;
            }

            if (!groupedByBssid.TryGetValue(mac.Canonical, out var entry))
            {
                entry = (mac, new List<ScannedWiFiNetwork>(2));
                groupedByBssid[mac.Canonical] = entry;
            }

            entry.Observations.Add(item);
        }

        var matched = new List<ScannedDroneNetworkDto>();
        foreach (var (_, (mac, observations)) in groupedByBssid)
        {
            ct.ThrowIfCancellationRequested();

            var orderedObs = observations
                .OrderByDescending(static o => o.SignalStrengthPercent ?? int.MinValue)
                .ThenByDescending(static o => o.RssiDbm ?? int.MinValue)
                .ThenByDescending(static o => TryCreateValidSsid(o.Ssid) is not null ? 1 : 0)
                .ThenBy(static o => o.InterfaceName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var primary = orderedObs[0];
            var bestSsid = orderedObs
                .Select(static o => TryCreateValidSsid(o.Ssid))
                .FirstOrDefault(static s => s is not null);

            var evaluation = EvaluateSingle(mac, bestSsid);
            if (!evaluation.IsDroneStation && !(includePossible && evaluation.Verdict == DetectionVerdict.Possible))
            {
                continue;
            }

            var signalPercent = primary.SignalStrengthPercent
                ?? orderedObs.Select(static o => o.SignalStrengthPercent).FirstOrDefault(static v => v.HasValue);
            var rssiDbm = primary.RssiDbm
                ?? orderedObs.Select(static o => o.RssiDbm).FirstOrDefault(static v => v.HasValue);
            var band = !string.IsNullOrWhiteSpace(primary.Band)
                ? primary.Band
                : orderedObs.Select(static o => o.Band).FirstOrDefault(static b => !string.IsNullOrWhiteSpace(b));
            var freqMhz = primary.FrequencyMhz
                ?? orderedObs.Select(static o => o.FrequencyMhz).FirstOrDefault(static f => f.HasValue);
            var channel = primary.Channel
                ?? orderedObs.Select(static o => o.Channel).FirstOrDefault(static c => c.HasValue);

            var interfaceNames = orderedObs
                .Where(static o => !string.IsNullOrWhiteSpace(o.InterfaceName))
                .Select(static o => o.InterfaceName!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            matched.Add(new ScannedDroneNetworkDto
            {
                Bssid = evaluation.NormalizedBssid,
                Ssid = evaluation.ObservedSsid ?? bestSsid?.Value,
                IsDroneStation = evaluation.IsDroneStation,
                Verdict = evaluation.Verdict,
                ConfidenceScore = evaluation.ConfidenceScore,
                BrandMark = evaluation.BrandMark,
                CandidateModels = evaluation.CandidateModels,
                FrequencyBands = evaluation.FrequencyBands,
                StationRole = evaluation.StationRole,
                SignalStrengthPercent = signalPercent,
                RssiDbm = rssiDbm,
                Band = band,
                FrequencyMhz = freqMhz,
                Channel = channel,
                InterfaceNames = interfaceNames
            });
        }

        var ordered = matched
            .OrderByDescending(static d => d.ConfidenceScore)
            .ThenByDescending(static d => d.SignalStrengthPercent ?? int.MinValue)
            .ThenBy(static d => d.Bssid, StringComparer.Ordinal)
            .ToArray();

        var scannedInterfaces = batch.ScannedInterfaces
            .Where(static i => !string.IsNullOrWhiteSpace(i))
            .Select(static i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static i => i, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ScannedDroneNetworksResponse
        {
            ScannedAtUtc = clock.GetUtcNow(),
            ScannedInterfaces = scannedInterfaces,
            TotalNetworksScanned = groupedByBssid.Count,
            DroneCount = ordered.Length,
            Drones = ordered
        };
    }

    private DroneNetworkEvaluation EvaluateSingle(MacAddress mac, SsidName? ssid)
    {
        var context = new EvidenceEvaluationContext(mac, ssid);
        var accumulator = new EvidenceAccumulator();
        foreach (var evaluator in evaluators)
        {
            evaluator.Evaluate(context, accumulator);
        }

        return scoringPolicy.EvaluateVerdict(mac, ssid, accumulator.ToResult());
    }

    private static SsidName? TryCreateValidSsid(string? rawSsid)
    {
        if (string.IsNullOrWhiteSpace(rawSsid))
        {
            return null;
        }

        return SsidName.TryCreate(rawSsid, out var ssid, out _) ? ssid : null;
    }

    private sealed class UnavailableWiFiScanner : IWiFiScanner
    {
        public Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default) =>
            Task.FromResult(new WlanInterfaceDiscoveryResult(false, "No Wi-Fi scanner is configured.", []));

        public Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default) =>
            throw ApiException.ServiceUnavailable("No Wi-Fi scanner is configured.");
    }
}
