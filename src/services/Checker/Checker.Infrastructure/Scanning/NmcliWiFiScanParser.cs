using System.Globalization;
using System.Text;
using Checker.Application.DTOs;

namespace Checker.Infrastructure.Scanning;

public static class NmcliWiFiScanParser
{
    public static IReadOnlyList<ScannedWiFiNetwork> Parse(string output, string? interfaceName = null)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var normalizedIface = string.IsNullOrWhiteSpace(interfaceName) ? null : interfaceName.Trim();
        var results = new List<ScannedWiFiNetwork>();
        using var reader = new StringReader(output);

        while (reader.ReadLine() is { } rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = SplitUnescapedColons(rawLine);
            if (fields.Count < 2 || string.IsNullOrWhiteSpace(fields[1]))
            {
                continue;
            }

            var ssid = string.IsNullOrWhiteSpace(fields[0]) ? null : fields[0].Trim();
            var bssid = fields[1].Trim();

            int? signal = fields.Count > 2 &&
                          int.TryParse(fields[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
                ? Math.Clamp(s, 0, 100)
                : null;

            int? channel = fields.Count > 3 &&
                           int.TryParse(fields[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ch)
                ? ch
                : null;

            int? explicitFreq = fields.Count > 4 ? ParseFreqMhz(fields[4]) : null;

            var (freqMhz, band) = WiFiChannelConverter.Resolve(channel, explicitFreq, null);
            int? rssi = signal.HasValue ? WiFiChannelConverter.SignalPercentToApproxRssiDbm(signal.Value) : null;

            results.Add(new ScannedWiFiNetwork(
                Bssid: bssid,
                Ssid: ssid,
                SignalStrengthPercent: signal,
                RssiDbm: rssi,
                Band: band,
                FrequencyMhz: freqMhz,
                Channel: channel,
                InterfaceName: normalizedIface));
        }

        return results;
    }

    /// <summary>
    /// Parses <c>nmcli -t -f DEVICE,TYPE,STATE,CONNECTION dev status</c> output and filters strictly to <c>wifi</c> devices.
    /// </summary>
    public static IReadOnlyList<WlanInterfaceInfo> ParseInterfaces(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var results = new List<WlanInterfaceInfo>();
        using var reader = new StringReader(output);

        while (reader.ReadLine() is { } rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = SplitUnescapedColons(rawLine);
            if (fields.Count < 3)
            {
                continue;
            }

            var device = fields[0].Trim();
            var type = fields[1].Trim();
            var rawState = fields[2].Trim();

            if (device.Length == 0 || !type.Equals("wifi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var (state, isUsable) = NormalizeNmcliState(rawState);
            results.Add(new WlanInterfaceInfo(
                Name: device,
                State: state,
                IsUsableForScan: isUsable,
                Description: "802.11 (NetworkManager)"));
        }

        return results;
    }

    private static (string State, bool IsUsable) NormalizeNmcliState(string rawState)
    {
        var lower = rawState.ToLowerInvariant();
        if (lower.StartsWith("connected", StringComparison.Ordinal))
        {
            return ("Connected", true);
        }

        if (lower.StartsWith("disconnected", StringComparison.Ordinal))
        {
            return ("Disconnected", true);
        }

        if (lower.StartsWith("connecting", StringComparison.Ordinal))
        {
            return ("Connecting", true);
        }

        if (lower.StartsWith("unavailable", StringComparison.Ordinal) ||
            lower.StartsWith("unmanaged", StringComparison.Ordinal))
        {
            return ("Unavailable", false);
        }

        return ("Unknown", false);
    }

    private static int? ParseFreqMhz(string raw)
    {
        var token = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mhz) ? mhz : null;
    }

    private static List<string> SplitUnescapedColons(string line)
    {
        var fields = new List<string>(6);
        var current = new StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '\\' && i + 1 < line.Length)
            {
                current.Append(line[++i]);
                continue;
            }

            if (ch == ':')
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
