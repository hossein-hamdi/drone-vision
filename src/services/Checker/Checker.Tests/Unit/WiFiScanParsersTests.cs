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

        Assert.Null(parsed[1].Ssid);
        Assert.Equal("60:60:1F:AA:BB:CC", parsed[1].Bssid);
        Assert.Equal(2437, parsed[1].FrequencyMhz);
        Assert.Equal("2.4 GHz", parsed[1].Band);
    }

    [Fact]
    public void NetshParser_ShouldTrackInterfaceNamesAcrossMultipleAdapters()
    {
        const string sample = """
            Interface name : Wi-Fi
            There are 1 networks currently visible.

            SSID 1 : Mavic-Air-2
                Network type            : Infrastructure
                Authentication          : WPA2-Personal
                Encryption              : CCMP
                BSSID 1                 : 60:60:1f:11:22:33
                     Signal             : 85%
                     Radio type         : 802.11ac
                     Band               : 5 GHz
                     Channel            : 149

            Interface name : Wi-Fi 2
            There are 1 networks currently visible.

            SSID 1 : SYMA_FPV_7788
                Network type            : Infrastructure
                Authentication          : Open
                Encryption              : None
                BSSID 1                 : 20:f4:1b:44:55:66
                     Signal             : 70%
                     Radio type         : 802.11n
                     Band               : 2.4 GHz
                     Channel            : 6
            """;

        var parsed = NetshWiFiScanParser.Parse(sample);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("Wi-Fi", parsed[0].InterfaceName);
        Assert.Equal("60:60:1f:11:22:33", parsed[0].Bssid);
        Assert.Equal("Wi-Fi 2", parsed[1].InterfaceName);
        Assert.Equal("20:f4:1b:44:55:66", parsed[1].Bssid);
    }

    [Fact]
    public void NetshWlanInterfacesParser_ShouldParseMultipleAdaptersAndHandleNoInterface()
    {
        const string sample = """
            There are 2 interfaces on the system:

                Name                   : Wi-Fi
                Description            : Intel(R) Wi-Fi 6 AX201 160MHz
                GUID                   : 11111111-1111-1111-1111-111111111111
                Physical address       : aa:bb:cc:dd:ee:01
                Interface type         : Primary
                State                  : connected

                Name                   : Wi-Fi 2
                Description            : Alfa AWUS036ACH USB Wireless Adapter
                GUID                   : 22222222-2222-2222-2222-222222222222
                Physical address       : aa:bb:cc:dd:ee:02
                Interface type         : Primary
                State                  : disconnected
            """;

        var interfaces = NetshWlanInterfacesParser.Parse(sample);

        Assert.Equal(2, interfaces.Count);
        Assert.Equal("Wi-Fi", interfaces[0].Name);
        Assert.Equal("Connected", interfaces[0].State);
        Assert.True(interfaces[0].IsUsableForScan);
        Assert.Equal("Intel(R) Wi-Fi 6 AX201 160MHz", interfaces[0].Description);
        Assert.Equal("AA:BB:CC:DD:EE:01", interfaces[0].MacAddress);

        Assert.Equal("Wi-Fi 2", interfaces[1].Name);
        Assert.Equal("Disconnected", interfaces[1].State);
        Assert.True(interfaces[1].IsUsableForScan);
        Assert.Equal("AA:BB:CC:DD:EE:02", interfaces[1].MacAddress);

        Assert.Empty(NetshWlanInterfacesParser.Parse("There is no wireless interface on the system."));
    }

    [Fact]
    public void IwParser_ShouldParseWlanInterfacesAndScanObservations()
    {
        const string iwDevSample = """
            phy#1
            	Unnamed/non-netdev interface
            		wdev 0x100000002
            		addr 12:34:56:78:9a:bc
            		type P2P-device
            	Interface wlan1
            		ifindex 5
            		wdev 0x100000001
            		addr 00:11:22:33:44:55
            		type managed
            		channel 6 (2437 MHz), width: 20 MHz
            phy#0
            	Interface wlan0
            		ifindex 3
            		wdev 0x1
            		addr aa:bb:cc:dd:ee:ff
            		type managed
            	Interface mon0
            		ifindex 4
            		wdev 0x2
            		addr aa:bb:cc:dd:ee:fe
            		type monitor
            """;

        var operStates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["wlan0"] = "up",
            ["wlan1"] = "down"
        };

        var interfaces = IwWiFiScanParser.ParseInterfaces(iwDevSample, operStates);

        Assert.Equal(2, interfaces.Count);
        Assert.Contains(interfaces, i => i.Name == "wlan0" && i.State == "Up" && i.IsUsableForScan && i.MacAddress == "AA:BB:CC:DD:EE:FF");
        Assert.Contains(interfaces, i => i.Name == "wlan1" && i.State == "Down" && !i.IsUsableForScan && i.MacAddress == "00:11:22:33:44:55");

        const string iwScanSample = """
            BSS 60:60:1f:aa:bb:cc(on wlan0)
            	last seen: 120 ms ago
            	freq: 5745
            	signal: -54.00 dBm
            	SSID: Mavic-3-Pro
            	DS Parameter set: channel 149
            BSS 20:f4:1b:11:22:33(on wlan0) -- associated
            	freq: 2437
            	signal: -68.00 dBm
            	SSID: \x00\x00\x00\x00
            BSS invalid-line-ignored
            """;

        var scanned = IwWiFiScanParser.ParseScanOutput(iwScanSample, "wlan0");

        Assert.Equal(2, scanned.Count);
        Assert.Equal("60:60:1F:AA:BB:CC", scanned[0].Bssid);
        Assert.Equal("Mavic-3-Pro", scanned[0].Ssid);
        Assert.Equal(5745, scanned[0].FrequencyMhz);
        Assert.Equal("5.8 GHz", scanned[0].Band);
        Assert.Equal(149, scanned[0].Channel);
        Assert.Equal(-54, scanned[0].RssiDbm);
        Assert.Equal(92, scanned[0].SignalStrengthPercent);
        Assert.Equal("wlan0", scanned[0].InterfaceName);

        Assert.Equal("20:F4:1B:11:22:33", scanned[1].Bssid);
        Assert.Null(scanned[1].Ssid);
        Assert.Equal(2437, scanned[1].FrequencyMhz);
        Assert.Equal("2.4 GHz", scanned[1].Band);
        Assert.Equal(6, scanned[1].Channel);
        Assert.Equal(-68, scanned[1].RssiDbm);
        Assert.Equal(64, scanned[1].SignalStrengthPercent);
        Assert.Equal("wlan0", scanned[1].InterfaceName);
    }

    [Fact]
    public void NmcliParser_ShouldFilterOnlyWifiInterfacesAndAttachInterfaceName()
    {
        const string devStatus = """
            lo:loopback:unmanaged:
            eth0:ethernet:connected:Wired connection 1
            docker0:bridge:connected (externally):docker0
            wlan0:wifi:connected:Mavic-3
            wlan1:wifi:disconnected:
            wlan2:wifi:unavailable:
            """;

        var interfaces = NmcliWiFiScanParser.ParseInterfaces(devStatus);

        Assert.Equal(3, interfaces.Count);
        Assert.Equal("wlan0", interfaces[0].Name);
        Assert.Equal("Connected", interfaces[0].State);
        Assert.True(interfaces[0].IsUsableForScan);

        Assert.Equal("wlan1", interfaces[1].Name);
        Assert.Equal("Disconnected", interfaces[1].State);
        Assert.True(interfaces[1].IsUsableForScan);

        Assert.Equal("wlan2", interfaces[2].Name);
        Assert.Equal("Unavailable", interfaces[2].State);
        Assert.False(interfaces[2].IsUsableForScan);

        var rows = NmcliWiFiScanParser.Parse(@"Bugs_5W_9921:20\:F4\:1B\:44\:55\:66:82:149:5745 MHz:WPA2", "wlan1");
        var single = Assert.Single(rows);
        Assert.Equal("wlan1", single.InterfaceName);
    }
}
