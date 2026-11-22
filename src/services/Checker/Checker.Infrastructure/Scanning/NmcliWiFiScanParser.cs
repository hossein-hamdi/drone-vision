using System.Globalization;
using System.Text;
using Checker.Application.DTOs;

namespace Checker.Infrastructure.Scanning;

public static class NmcliWiFiScanParser
{
    public static IReadOnlyList<ScannedWiFiNetwork> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

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
            var security = fields.Count > 5 && !string.IsNullOrWhiteSpace(fields[5]) ? fields[5].Trim() : null;

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
                RadioType: null,
                Authentication: security));
        }

        return results;
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
