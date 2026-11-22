namespace Checker.Infrastructure.Scanning;

public static class WiFiChannelConverter
{
    public static (int? FrequencyMhz, string? Band) Resolve(int? channel, int? explicitFrequencyMhz, string? rawBand)
    {
        if (explicitFrequencyMhz is > 0)
        {
            return (explicitFrequencyMhz, ResolveBandFromFrequency(explicitFrequencyMhz.Value, rawBand));
        }

        if (channel is null or <= 0)
        {
            return (null, NormalizeBandLabel(rawBand));
        }

        var ch = channel.Value;
        var is6Ghz = rawBand is not null && rawBand.Contains("6 GHz", StringComparison.OrdinalIgnoreCase);
        if (is6Ghz && ch <= 233)
        {
            return (5950 + ch * 5, "6 GHz");
        }

        if (ch is >= 1 and <= 13)
        {
            return (2407 + ch * 5, "2.4 GHz");
        }

        if (ch == 14)
        {
            return (2484, "2.4 GHz");
        }

        if (ch is >= 32 and <= 177)
        {
            var freq = 5000 + ch * 5;
            return (freq, freq >= 5725 ? "5.8 GHz" : "5 GHz");
        }

        return (null, NormalizeBandLabel(rawBand));
    }

    public static int SignalPercentToApproxRssiDbm(int signalPercent) =>
        Math.Clamp(signalPercent, 0, 100) / 2 - 100;

    private static string ResolveBandFromFrequency(int freqMhz, string? rawBand) => freqMhz switch
    {
        >= 2400 and <= 2499 => "2.4 GHz",
        >= 5150 and < 5725 => "5 GHz",
        >= 5725 and <= 5895 => "5.8 GHz",
        >= 5925 and <= 7125 => "6 GHz",
        _ => NormalizeBandLabel(rawBand) ?? $"{freqMhz} MHz"
    };

    private static string? NormalizeBandLabel(string? rawBand) =>
        string.IsNullOrWhiteSpace(rawBand) ? null : rawBand.Trim();
}
