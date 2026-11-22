using System.Globalization;
using System.Text.RegularExpressions;
using Checker.Application.DTOs;

namespace Checker.Infrastructure.Scanning;

public static partial class NetshWiFiScanParser
{
    [GeneratedRegex(@"^SSID\s+\d+\s*:\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SsidHeaderRegex();

    [GeneratedRegex(@"^BSSID\s+\d+\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BssidHeaderRegex();

    public static IReadOnlyList<ScannedWiFiNetwork> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var results = new List<ScannedWiFiNetwork>();
        string? currentSsid = null;
        string? currentAuth = null;

        string? currentBssid = null;
        int? currentSignal = null;
        string? currentRadio = null;
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
                RadioType: currentRadio,
                Authentication: currentAuth));

            currentBssid = null;
            currentSignal = null;
            currentRadio = null;
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

            var ssidMatch = SsidHeaderRegex().Match(line);
            if (ssidMatch.Success)
            {
                FlushBssid();
                var rawSsid = ssidMatch.Groups[1].Value.Trim();
                currentSsid = rawSsid.Length == 0 ? null : rawSsid;
                currentAuth = null;
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
                if (key.Equals("Authentication", StringComparison.OrdinalIgnoreCase))
                {
                    currentAuth = value.Length == 0 ? null : value;
                }

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
            else if (key.Equals("Radio type", StringComparison.OrdinalIgnoreCase))
            {
                currentRadio = value.Length == 0 ? null : value;
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
