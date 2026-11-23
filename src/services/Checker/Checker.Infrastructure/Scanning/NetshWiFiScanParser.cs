using System.Globalization;
using System.Text.RegularExpressions;
using Checker.Application.DTOs;
using Checker.Domain.ValueObjects;

namespace Checker.Infrastructure.Scanning;

public static partial class NetshWiFiScanParser
{
    [GeneratedRegex(@"^Interface name\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceNameRegex();

    [GeneratedRegex(@"^SSID\s+\d+\s*:\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SsidHeaderRegex();

    [GeneratedRegex(@"^BSSID\s+\d+\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BssidHeaderRegex();

    public static IReadOnlyList<ScannedWiFiNetwork> Parse(string output, string? defaultInterfaceName = null)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var results = new List<ScannedWiFiNetwork>();
        var currentInterface = string.IsNullOrWhiteSpace(defaultInterfaceName) ? null : defaultInterfaceName.Trim();
        string? currentSsid = null;

        string? currentBssid = null;
        int? currentSignal = null;
        string? currentBand = null;
        int? currentChannel = null;

        void FlushBssid()
        {
            if (string.IsNullOrWhiteSpace(currentBssid))
            {
                return;
            }

            var (freqMhz, resolvedBand) = WiFiChannelConverter.Resolve(currentChannel, null, currentBand);
            int? rssi = currentSignal.HasValue
                ? WiFiChannelConverter.SignalPercentToApproxRssiDbm(currentSignal.Value)
                : null;

            results.Add(new ScannedWiFiNetwork(
                Bssid: currentBssid,
                Ssid: string.IsNullOrWhiteSpace(currentSsid) ? null : currentSsid,
                SignalStrengthPercent: currentSignal,
                RssiDbm: rssi,
                Band: resolvedBand,
                FrequencyMhz: freqMhz,
                Channel: currentChannel,
                InterfaceName: currentInterface));

            currentBssid = null;
            currentSignal = null;
            currentBand = null;
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

            var ifaceMatch = InterfaceNameRegex().Match(line);
            if (ifaceMatch.Success)
            {
                FlushBssid();
                var rawIface = ifaceMatch.Groups[1].Value.Trim();
                currentInterface = rawIface.Length == 0 ? currentInterface : rawIface;
                currentSsid = null;
                continue;
            }

            var ssidMatch = SsidHeaderRegex().Match(line);
            if (ssidMatch.Success)
            {
                FlushBssid();
                var rawSsid = ssidMatch.Groups[1].Value.Trim();
                currentSsid = rawSsid.Length == 0 ? null : rawSsid;
                continue;
            }

            var bssidMatch = BssidHeaderRegex().Match(line);
            if (bssidMatch.Success)
            {
                FlushBssid();
                currentBssid = bssidMatch.Groups[1].Value.Trim();
                continue;
            }

            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0)
            {
                continue;
            }

            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim();

            if (currentBssid is null)
            {
                continue;
            }

            if (key.Equals("Signal", StringComparison.OrdinalIgnoreCase))
            {
                var cleaned = value.TrimEnd('%', ' ');
                if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct))
                {
                    currentSignal = Math.Clamp(pct, 0, 100);
                }
            }
            else if (key.Equals("Band", StringComparison.OrdinalIgnoreCase))
            {
                currentBand = value.Length == 0 ? null : value;
            }
            else if (key.Equals("Channel", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ch))
                {
                    currentChannel = ch;
                }
            }
        }

        FlushBssid();
        return results;
    }
}

/// <summary>
/// Parses Windows <c>netsh wlan show interfaces</c> output into normalized WLAN adapter records.
/// </summary>
public static class NetshWlanInterfacesParser
{
    public static IReadOnlyList<WlanInterfaceInfo> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output) ||
            output.Contains("There is no wireless interface", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("is not running", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var results = new List<WlanInterfaceInfo>();
        string? name = null;
        string? description = null;
        string? macAddress = null;
        string? state = null;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var (normalizedState, isUsable) = NormalizeState(state);
            results.Add(new WlanInterfaceInfo(
                Name: name,
                State: normalizedState,
                IsUsableForScan: isUsable,
                Description: description,
                MacAddress: macAddress));

            name = null;
            description = null;
            macAddress = null;
            state = null;
        }

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0)
            {
                continue;
            }

            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim();

            if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                name = value.Length == 0 ? null : value;
                continue;
            }

            if (name is null)
            {
                continue;
            }

            if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
            {
                description = value.Length == 0 ? null : value;
            }
            else if (key.Equals("Physical address", StringComparison.OrdinalIgnoreCase))
            {
                if (MacAddress.TryParse(value, null, out var parsed))
                {
                    macAddress = parsed.Canonical;
                }
            }
            else if (key.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                state = value.Length == 0 ? null : value;
            }
        }

        Flush();
        return results;
    }

    private static (string State, bool IsUsable) NormalizeState(string? rawState)
    {
        if (string.IsNullOrWhiteSpace(rawState))
        {
            return ("Unknown", true);
        }

        return rawState.Trim().ToLowerInvariant() switch
        {
            "connected" => ("Connected", true),
            "disconnected" => ("Disconnected", true),
            "associating" or "authenticating" => ("Connecting", true),
            "disabled" or "unavailable" => ("Unavailable", false),
            _ => ("Unknown", true)
        };
    }
}
