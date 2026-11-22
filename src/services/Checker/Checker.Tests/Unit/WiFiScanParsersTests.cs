using Checker.Infrastructure.Scanning;

namespace Checker.Tests.Unit;

public sealed class WiFiScanParsersTests
{
    [Fact]
    public void NetshParser_ShouldParseMultipleSsidsBssidsHiddenNetworksAndRfMetrics()
    {
        const string sample = """
            Interface name : Wi-Fi
            There are 3 networks currently visible.

            SSID 1 : HomeRouter
                Network type            : Infrastructure
                Authentication          : WPA2-Personal
                Encryption              : CCMP
                BSSID 1                 : 1a:6d:cb:56:a8:65
                     Signal             : 96%
                     Radio type         : 802.11ac
                     Band               : 2.4 GHz
                     Channel            : 11

            SSID 2 : SYMA_FPV_4120
                Network type            : Infrastructure
                Authentication          : Open
                Encryption              : None
                BSSID 1                 : 20:f4:1b:10:20:30
                     Signal             : 78%
                     Radio type         : 802.11n
                     Band               : 2.4 GHz
                     Channel            : 6
                BSSID 2                 : 60:60:1f:aa:bb:cc
                     Signal             : 90%
                     Radio type         : 802.11ac
                     Band               : 5 GHz
                     Channel            : 149

            SSID 3 :
                Network type            : Infrastructure
                Authentication          : WPA2-Personal
                Encryption              : CCMP
                BSSID 1                 : ec:5b:cd:e1:22:33
                     Signal             : 64%
                     Radio type         : 802.11ax
                     Band               : 5 GHz
                     Channel            : 36
            """;

        var parsed = NetshWiFiScanParser.Parse(sample);

        Assert.Equal(4, parsed.Count);

        Assert.Equal("HomeRouter", parsed[0].Ssid);
        Assert.Equal("1a:6d:cb:56:a8:65", parsed[0].Bssid);
        Assert.Equal(96, parsed[0].SignalStrengthPercent);
        Assert.Equal(2462, parsed[0].FrequencyMhz);
        Assert.Equal("2.4 GHz", parsed[0].Band);
        Assert.Equal(11, parsed[0].Channel);

        Assert.Equal("SYMA_FPV_4120", parsed[1].Ssid);
        Assert.Equal("Open", parsed[1].Authentication);
        Assert.Equal(2437, parsed[1].FrequencyMhz);

        Assert.Equal("60:60:1f:aa:bb:cc", parsed[2].Bssid);
        Assert.Equal(5745, parsed[2].FrequencyMhz);
        Assert.Equal("5.8 GHz", parsed[2].Band);

        Assert.Null(parsed[3].Ssid);
        Assert.Equal("ec:5b:cd:e1:22:33", parsed[3].Bssid);
        Assert.Equal(5180, parsed[3].FrequencyMhz);
        Assert.Equal("5 GHz", parsed[3].Band);
    }

    [Fact]
    public void NmcliParser_ShouldParseEscapedColonsAndFrequencies()
    {
        const string sample = """
            Bugs_5W_9921:20\:F4\:1B\:44\:55\:66:82:149:5745 MHz:WPA2
            :60\:60\:1F\:AA\:BB\:CC:95:6:2437 MHz:WPA2
            """;

        var parsed = NmcliWiFiScanParser.Parse(sample);

        Assert.Equal(2, parsed.Count);

        Assert.Equal("Bugs_5W_9921", parsed[0].Ssid);
        Assert.Equal("20:F4:1B:44:55:66", parsed[0].Bssid);
        Assert.Equal(82, parsed[0].SignalStrengthPercent);
        Assert.Equal(149, parsed[0].Channel);
        Assert.Equal(5745, parsed[0].FrequencyMhz);
        Assert.Equal("5.8 GHz", parsed[0].Band);
        Assert.Equal("WPA2", parsed[0].Authentication);

        Assert.Null(parsed[1].Ssid);
        Assert.Equal("60:60:1F:AA:BB:CC", parsed[1].Bssid);
        Assert.Equal(2437, parsed[1].FrequencyMhz);
        Assert.Equal("2.4 GHz", parsed[1].Band);
    }
}
