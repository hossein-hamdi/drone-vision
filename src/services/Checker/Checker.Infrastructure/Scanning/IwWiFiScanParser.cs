using System.Globalization;
using System.Text.RegularExpressions;
using Checker.Application.DTOs;
using Checker.Domain.ValueObjects;

namespace Checker.Infrastructure.Scanning;

/// <summary>
/// Parses output from Linux <c>iw dev</c> and <c>iw dev &lt;iface&gt; scan</c> commands.
/// </summary>
public static partial class IwWiFiScanParser
{
    [GeneratedRegex(@"^BSS\s+([0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5})\b", RegexOptions.CultureInvariant)]
    private static partial Regex BssHeaderRegex();

    [GeneratedRegex(@"^freq:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FreqRegex();

    [GeneratedRegex(@"^signal:\s*(-?\d+(?:\.\d+)?)\s*dBm", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SignalRegex();

    [GeneratedRegex(@"^DS Parameter set:\s*channel\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DsChannelRegex();

    [GeneratedRegex(@"^\*\s*primary channel:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtPrimaryChannelRegex();

    [GeneratedRegex(@"^(?:\\x00)+$", RegexOptions.CultureInvariant)]
    private static partial Regex NullEscapedSsidRegex();

    public static IReadOnlyList<WlanInterfaceInfo> ParseInterfaces(
        string iwDevOutput,
        IReadOnlyDictionary<string, string>? operStates = null)
    {
        if (string.IsNullOrWhiteSpace(iwDevOutput))
        {
            return [];
        }

        var results = new List<WlanInterfaceInfo>();
        string? currentName = null;
        string? currentMac = null;
        string? currentType = null;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(currentName))
            {
                return;
            }

            if (string.Equals(currentType, "monitor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentType, "P2P-device", StringComparison.OrdinalIgnoreCase))
            {
                currentName = null;
                currentMac = null;
                currentType = null;
                return;
            }

            string? rawOper = null;
            operStates?.TryGetValue(currentName, out rawOper);
            var (normalizedState, isUsable) = NormalizeLinuxState(rawOper);

            results.Add(new WlanInterfaceInfo(
                Name: currentName,
                State: normalizedState,
                IsUsableForScan: isUsable,
                Description: string.IsNullOrWhiteSpace(currentType) ? null : $"802.11 ({currentType})",
                MacAddress: currentMac));

            currentName = null;
            currentMac = null;
            currentType = null;
        }

        using var reader = new StringReader(iwDevOutput);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("phy#", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Unnamed/", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                continue;
            }

            if (line.StartsWith("Interface ", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                var candidate = line["Interface ".Length..].Trim();
                currentName = candidate.Length == 0 ? null : candidate;
                continue;
            }

            if (currentName is null)
            {
                continue;
            }

            if (line.StartsWith("addr ", StringComparison.OrdinalIgnoreCase))
            {
                var rawMac = line["addr ".Length..].Trim();
                if (MacAddress.TryParse(rawMac, null, out var parsedMac))
                {
                    currentMac = parsedMac.Canonical;
                }
            }
            else if (line.StartsWith("type ", StringComparison.OrdinalIgnoreCase))
            {
                currentType = line["type ".Length..].Trim();
            }
        }

        Flush();
        return results;
    }

    public static IReadOnlyList<ScannedWiFiNetwork> ParseScanOutput(string output, string interfaceName)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var results = new List<ScannedWiFiNetwork>();
        string? currentBssid = null;
        string? currentSsid = null;
        int? currentFreqMhz = null;
        int? currentRssiDbm = null;
        int? currentChannel = null;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(currentBssid))
            {
                return;
            }

            var resolvedChannel = currentChannel ?? (currentFreqMhz.HasValue
                ? WiFiChannelConverter.FrequencyMhzToChannel(currentFreqMhz.Value)
                : null);

            var (freqMhz, band) = WiFiChannelConverter.Resolve(resolvedChannel, currentFreqMhz, null);
            int? signalPercent = currentRssiDbm.HasValue
                ? WiFiChannelConverter.RssiDbmToApproxSignalPercent(currentRssiDbm.Value)
                : null;

            results.Add(new ScannedWiFiNetwork(
                Bssid: currentBssid,
                Ssid: currentSsid,
                SignalStrengthPercent: signalPercent,
                RssiDbm: currentRssiDbm,
                Band: band,
                FrequencyMhz: freqMhz,
                Channel: resolvedChannel,
                InterfaceName: interfaceName));

            currentBssid = null;
            currentSsid = null;
            currentFreqMhz = null;
            currentRssiDbm = null;
            currentChannel = null;
        }

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("BSS ", StringComparison.Ordinal))
            {
                Flush();
                var bssMatch = BssHeaderRegex().Match(line);
                if (bssMatch.Success)
                {
                    currentBssid = bssMatch.Groups[1].Value.ToUpperInvariant();
                }

                continue;
            }

            if (currentBssid is null)
            {
                continue;
            }

            if (line.StartsWith("SSID:", StringComparison.Ordinal))
            {
                var rawSsid = line["SSID:".Length..].Trim();
                currentSsid = string.IsNullOrWhiteSpace(rawSsid) || NullEscapedSsidRegex().IsMatch(rawSsid)
                    ? null
                    : rawSsid;
                continue;
            }

            var freqMatch = FreqRegex().Match(line);
            if (freqMatch.Success &&
                int.TryParse(freqMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var freq))
            {
                currentFreqMhz = freq;
                continue;
            }

            var signalMatch = SignalRegex().Match(line);
            if (signalMatch.Success &&
                double.TryParse(signalMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbm))
            {
                currentRssiDbm = (int)Math.Round(dbm, MidpointRounding.AwayFromZero);
                continue;
            }

            var dsMatch = DsChannelRegex().Match(line);
            if (dsMatch.Success &&
                int.TryParse(dsMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dsChannel))
            {
                currentChannel = dsChannel;
                continue;
            }

            if (currentChannel is null)
            {
                var htMatch = HtPrimaryChannelRegex().Match(line);
                if (htMatch.Success &&
                    int.TryParse(htMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var htChannel))
                {
                    currentChannel = htChannel;
                }
            }
        }

        Flush();
        return results;
    }

    private static (string State, bool IsUsable) NormalizeLinuxState(string? rawState)
    {
        if (string.IsNullOrWhiteSpace(rawState))
        {
            return ("Unknown", true);
        }

        return rawState.Trim().ToLowerInvariant() switch
        {
            "up" => ("Up", true),
            "dormant" => ("Disconnected", true),
            "disconnected" => ("Disconnected", true),
            "down" => ("Down", false),
            "unavailable" or "disabled" => ("Unavailable", false),
            _ => ("Unknown", true)
        };
    }
}
