using Checker.Infrastructure.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Checker.Tests.Unit;

public sealed class PlatformWiFiScannerSafetyTests
{
    [Theory]
    [InlineData("wlan0", true)]
    [InlineData("wlp3s0", true)]
    [InlineData("wlx00c0ca99a1b2", true)]
    [InlineData("Wi-Fi", true)]
    [InlineData("Wi-Fi 2", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("-wlan0", false)]
    [InlineData("--rescan", false)]
    [InlineData("wlan0;rm -rf /", false)]
    [InlineData("wlan0\nscan", false)]
    [InlineData("$(whoami)", false)]
    [InlineData("wlan0|cat", false)]
    public void IsValidInterfaceName_ShouldRejectUnsafeOrInjectionInputs(string? candidate, bool expected)
    {
        Assert.Equal(expected, PlatformWiFiScanner.IsValidInterfaceName(candidate));
    }

    [Fact]
    public void IsValidInterfaceName_ShouldRejectOversizedNamesAbove64Chars()
    {
        var tooLong = new string('w', 65);
        Assert.False(PlatformWiFiScanner.IsValidInterfaceName(tooLong));
        Assert.True(PlatformWiFiScanner.IsValidInterfaceName(new string('w', 64)));
    }

    [Fact]
    public void ReadLinuxSysfsWirelessInterfaces_ShouldExcludeEthernetLoopbackAndBridgeAndIncludeOnlyWlan()
    {
        var tempRoot = Directory.CreateTempSubdirectory("checker-sysfs-test-");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "lo"));
            Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "eth0"));
            Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "docker0"));

            var wlan0 = Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "wlan0"));
            Directory.CreateDirectory(Path.Combine(wlan0.FullName, "wireless"));
            File.WriteAllText(Path.Combine(wlan0.FullName, "flags"), "0x1003\n");
            File.WriteAllText(Path.Combine(wlan0.FullName, "operstate"), "up\n");
            File.WriteAllText(Path.Combine(wlan0.FullName, "address"), "aa:bb:cc:dd:ee:01\n");

            var wlan1 = Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "wlan1"));
            Directory.CreateDirectory(Path.Combine(wlan1.FullName, "phy80211"));
            File.WriteAllText(Path.Combine(wlan1.FullName, "flags"), "0x1003\n");
            File.WriteAllText(Path.Combine(wlan1.FullName, "operstate"), "down\n");
            File.WriteAllText(Path.Combine(wlan1.FullName, "address"), "aa:bb:cc:dd:ee:02\n");

            var wlan2 = Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "wlan2"));
            Directory.CreateDirectory(Path.Combine(wlan2.FullName, "wireless"));
            File.WriteAllText(Path.Combine(wlan2.FullName, "flags"), "0x1002\n");
            File.WriteAllText(Path.Combine(wlan2.FullName, "operstate"), "down\n");

            var discovered = PlatformWiFiScanner.ReadLinuxSysfsWirelessInterfaces(tempRoot.FullName);

            Assert.Equal(3, discovered.Count);
            Assert.False(discovered.ContainsKey("lo"));
            Assert.False(discovered.ContainsKey("eth0"));
            Assert.False(discovered.ContainsKey("docker0"));

            Assert.Equal("up", discovered["wlan0"].State);
            Assert.True(discovered["wlan0"].IsUsableForScan);
            Assert.Equal("AA:BB:CC:DD:EE:01", discovered["wlan0"].MacAddress);

            // Unassociated IFF_UP adapter (operstate=down, IFF_UP=1) remains scan-usable.
            Assert.Equal("disconnected", discovered["wlan1"].State);
            Assert.True(discovered["wlan1"].IsUsableForScan);

            // Administratively down adapter (IFF_UP=0) is not scan-usable.
            Assert.Equal("down", discovered["wlan2"].State);
            Assert.False(discovered["wlan2"].IsUsableForScan);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DiscoverLinuxInterfacesFromSysfs_WhenEmptyAndNoDbus_ShouldReturnUnavailableWithoutLeakingPaths()
    {
        var tempRoot = Directory.CreateTempSubdirectory("checker-win-docker-sim-");
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "lo"));
            Directory.CreateDirectory(Path.Combine(tempRoot.FullName, "eth0"));

            var scanner = new PlatformWiFiScanner(NullLogger<PlatformWiFiScanner>.Instance);
            var discovery = await scanner.DiscoverLinuxInterfacesForDiagnosticsAsync(
                tempRoot.FullName,
                hasDbus: false,
                CancellationToken.None);

            Assert.False(discovery.Available);
            Assert.Empty(discovery.Interfaces);
            Assert.Equal(PlatformWiFiScanner.UnavailableReason, discovery.Reason);
            Assert.DoesNotContain("/sys/class/net", discovery.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nmcli", discovery.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }
}
