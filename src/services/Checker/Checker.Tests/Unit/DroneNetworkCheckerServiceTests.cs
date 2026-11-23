using System.Diagnostics;
using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Checker.Application.Services;
using Checker.Domain.Enums;
using Checker.Domain.ValueObjects;
using Checker.Infrastructure.Catalog;

namespace Checker.Tests.Unit;

public sealed class DroneNetworkCheckerServiceTests
{
    private readonly IDroneSignatureCatalog _catalog = new EmbeddedDroneSignatureCatalog();

    private DroneNetworkCheckerService CreateService(IWiFiScanner? scanner = null) => new(
        new MacPrefixEvidenceEvaluator(_catalog),
        new SsidEvidenceEvaluator(_catalog),
        new VerdictScoringPolicy(),
        scanner);

    [Fact]
    public async Task CheckAsync_DedicatedDjiOuiWithoutSsid_ShouldConfirmDrone()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("60:60:1F:AA:BB:CC", null));

        Assert.True(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, result.Verdict);
        Assert.Equal("60:60:1F:AA:BB:CC", result.Bssid);
        Assert.Null(result.Ssid);
        Assert.Equal("DJI", result.BrandMark);
        Assert.Contains("2.4 GHz", result.FrequencyBands);
        Assert.Contains("5.8 GHz", result.FrequencyBands);
        Assert.NotEmpty(result.CandidateModels);
        Assert.Equal(StationRole.MultiRole, result.StationRole);
    }

    [Fact]
    public async Task CheckAsync_LongestPrefix28BitMaM_ShouldMatchAutel()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("EC:5B:CD:E4:11:22", null));

        Assert.True(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, result.Verdict);
        Assert.Equal("Autel Robotics", result.BrandMark);
    }

    [Fact]
    public async Task CheckAsync_SharedChipsetWithSymaSsid_ShouldConfirmDrone()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("20:F4:1B:11:22:33", "SYMA_FPV_8821"));

        Assert.True(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, result.Verdict);
        Assert.Equal("SYMA_FPV_8821", result.Ssid);
        Assert.Equal("Syma", result.BrandMark);
        Assert.True(result.ConfidenceScore >= 85);
    }

    [Fact]
    public async Task CheckAsync_SharedChipsetWithBugsSsid_ShouldConfirmMjx()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("24:0A:C4:11:22:33", "Bugs_7741"));

        Assert.True(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, result.Verdict);
        Assert.Equal("MJX", result.BrandMark);
    }

    [Fact]
    public async Task CheckAsync_SharedChipsetWithoutSsid_ShouldRemainPossibleNotConfirmed()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("24:0A:C4:11:22:33", null));

        Assert.False(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.Possible, result.Verdict);
        Assert.True(result.ConfidenceScore <= 40);
    }

    [Fact]
    public async Task CheckAsync_UnknownOuiWithoutDroneSsid_ShouldReturnNotRecognized()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("00:2A:6A:01:02:03", "HomeWiFi"));

        Assert.False(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.NotRecognized, result.Verdict);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public async Task CheckAsync_LocallyAdministeredMacWithDroneSsid_ShouldAttributeBySsid()
    {
        var sut = CreateService();
        var result = await sut.CheckAsync(new DroneNetworkCheckQuery("62:60:1F:11:22:33", "TELLO-9F2A1B"));

        Assert.True(result.IsDroneStation);
        Assert.Equal(DetectionVerdict.Probable, result.Verdict);
        Assert.Equal("TELLO-9F2A1B", result.Ssid);
    }

    [Fact]
    public void SsidName_ShouldEnforceMax32Utf8BytesAndNoControlChars()
    {
        Assert.True(SsidName.TryCreate("TELLO-123456", out var valid, out _));
        Assert.Equal("TELLO-123456", valid!.Value);

        Assert.False(SsidName.TryCreate(new string('A', 33), out _, out _));
        Assert.False(SsidName.TryCreate("DJI\nTest", out _, out _));
        Assert.False(SsidName.TryCreate("   ", out _, out _));
        Assert.False(SsidName.TryCreate("", out _, out _));
    }

    [Fact]
    public async Task CheckAsync_InvalidBssid_ShouldThrowValidationApiException()
    {
        var sut = CreateService();
        await Assert.ThrowsAsync<ApiException>(() =>
            sut.CheckAsync(new DroneNetworkCheckQuery("invalid-bssid", null)));
    }

    [Fact]
    public async Task CheckAsync_WarmSingleLookup_ShouldCompleteWellUnder50Ms()
    {
        var sut = CreateService();
        await sut.CheckAsync(new DroneNetworkCheckQuery("60:60:1F:AA:BB:CC", "Mavic-1234"));

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            await sut.CheckAsync(new DroneNetworkCheckQuery("20:F4:1B:11:22:33", "SYMA_FPV_8821"));
        }
        sw.Stop();

        Assert.True(sw.Elapsed.TotalMilliseconds / 100 < 50.0);
    }

    [Fact]
    public async Task ScanDroneNetworksAsync_ShouldFilterScannedWiFiListAndPreserveRfMetrics()
    {
        var fakeScanner = new StubWiFiScanner(
        [
            new ScannedWiFiNetwork("1a:6d:cb:56:a8:65", "Reichstag", 100, -33, "2.4 GHz", 2462, 11, "wlan0"),
            new ScannedWiFiNetwork("24:0a:c4:11:22:33", "SmartPlug-IoT", 60, -70, "2.4 GHz", 2412, 1, "wlan0"),
            new ScannedWiFiNetwork("20:f4:1b:55:66:77", "SYMA_FPV_9911", 84, -58, "2.4 GHz", 2437, 6, "wlan0"),
            new ScannedWiFiNetwork("60:60:1f:aa:bb:cc", "Mavic-3-8811", 92, -54, "5.8 GHz", 5745, 149, "wlan0"),
            new ScannedWiFiNetwork("ec:5b:cd:e1:22:33", null, 45, -77, "5.8 GHz", 5785, 157, "wlan0")
        ]);

        var sut = CreateService(fakeScanner);
        var result = await sut.ScanDroneNetworksAsync(includePossible: false);

        Assert.Equal(5, result.TotalNetworksScanned);
        Assert.Equal(3, result.DroneCount);
        Assert.Equal(3, result.Drones.Count);
        Assert.DoesNotContain(result.Drones, d => d.Bssid == "1A:6D:CB:56:A8:65");
        Assert.DoesNotContain(result.Drones, d => d.Bssid == "24:0A:C4:11:22:33");

        var dji = result.Drones.First(d => d.BrandMark == "DJI");
        Assert.Equal("Mavic-3-8811", dji.Ssid);
        Assert.Equal(92, dji.SignalStrengthPercent);
        Assert.Equal(-54, dji.RssiDbm);
        Assert.Equal("5.8 GHz", dji.Band);
        Assert.Equal(5745, dji.FrequencyMhz);
        Assert.Equal(149, dji.Channel);
        Assert.Equal(["wlan0"], dji.InterfaceNames);

        var withPossible = await sut.ScanDroneNetworksAsync(includePossible: true);
        Assert.Equal(4, withPossible.DroneCount);
    }

    [Fact]
    public async Task ScanDroneNetworksAsync_ShouldIncludeMjxBugs16ProDrone4GSsidEvenWithUnknownOrLaaBssid()
    {
        var fakeScanner = new StubWiFiScanner(
        [
            new ScannedWiFiNetwork("1a:6d:cb:56:a8:65", "HomeWiFi-5G", 95, -52, "5 GHz", 5180, 36, "wlan0"),
            new ScannedWiFiNetwork("02:11:22:33:44:55", "Drone4G-300a400", 88, -56, "5.8 GHz", 5745, 149, "wlan0")
        ]);

        var sut = CreateService(fakeScanner);
        var result = await sut.ScanDroneNetworksAsync(includePossible: false);

        Assert.Equal(2, result.TotalNetworksScanned);
        var drone = Assert.Single(result.Drones);
        Assert.Equal("Drone4G-300a400", drone.Ssid);
        Assert.Equal("MJX", drone.BrandMark);
        Assert.True(drone.IsDroneStation);
    }

    [Fact]
    public async Task ScanDroneNetworksAsync_ShouldDeduplicateAcrossMultipleWlanInterfacesAndMergeBestEvidence()
    {
        var fakeScanner = new StubWiFiScanner(
            ["wlan0", "wlan1"],
            [
                new ScannedWiFiNetwork("00:2A:6A:11:22:33", "OfficeRouter", 95, -52, "2.4 GHz", 2412, 1, "wlan0"),
                new ScannedWiFiNetwork("00-2a-6a-11-22-33", "OfficeRouter", 80, -60, "2.4 GHz", 2412, 1, "wlan1"),
                new ScannedWiFiNetwork("60:60:1f:aa:bb:cc", "Mavic-3-8811", 70, -65, "5.8 GHz", 5745, 149, "wlan0"),
                new ScannedWiFiNetwork("60-60-1F-AA-BB-CC", "Mavic-3-8811", 92, -54, "5.8 GHz", 5745, 149, "wlan1"),
                new ScannedWiFiNetwork("20:f4:1b:55:66:77", null, 88, -56, "2.4 GHz", 2437, 6, "wlan0"),
                new ScannedWiFiNetwork("20:f4:1b:55:66:77", "SYMA_FPV_9911", 62, -69, "2.4 GHz", 2437, 6, "wlan1"),
                new ScannedWiFiNetwork("60:60:1f:99:88:77", "Mavic-Dual", 65, -67, "2.4 GHz", 2412, 1, "wlan1")
            ]);

        var sut = CreateService(fakeScanner);
        var result = await sut.ScanDroneNetworksAsync(includePossible: false);

        Assert.Equal(["wlan0", "wlan1"], result.ScannedInterfaces);
        Assert.Equal(4, result.TotalNetworksScanned);
        Assert.Equal(3, result.DroneCount);

        var djiBest = result.Drones.Single(d => d.Bssid == "60:60:1F:AA:BB:CC");
        Assert.Equal(92, djiBest.SignalStrengthPercent);
        Assert.Equal(-54, djiBest.RssiDbm);
        Assert.Equal(["wlan0", "wlan1"], djiBest.InterfaceNames);

        var symaMerged = result.Drones.Single(d => d.Bssid == "20:F4:1B:55:66:77");
        Assert.True(symaMerged.IsDroneStation);
        Assert.Equal(DetectionVerdict.Confirmed, symaMerged.Verdict);
        Assert.Equal("Syma", symaMerged.BrandMark);
        Assert.Equal("SYMA_FPV_9911", symaMerged.Ssid);
        Assert.Equal(88, symaMerged.SignalStrengthPercent);
        Assert.Equal(["wlan0", "wlan1"], symaMerged.InterfaceNames);

        Assert.Single(result.Drones, d => d.Bssid == "60:60:1F:99:88:77");
    }

    [Fact]
    public async Task GetWlanInterfacesAsync_WhenAvailableAndUnavailable_ShouldReturnExpectedAvailability()
    {
        var availableScanner = new StubWiFiScanner(["wlan0", "wlan1"], []);
        var availableSut = CreateService(availableScanner);

        var availableResult = await availableSut.GetWlanInterfacesAsync();
        Assert.True(availableResult.Available);
        Assert.Null(availableResult.Reason);
        Assert.Equal(["wlan0", "wlan1"], availableResult.Interfaces.Select(static i => i.Name).ToArray());

        var unavailableSut = CreateService();
        var unavailableResult = await unavailableSut.GetWlanInterfacesAsync();
        Assert.False(unavailableResult.Available);
        Assert.False(string.IsNullOrWhiteSpace(unavailableResult.Reason));
        Assert.Empty(unavailableResult.Interfaces);
    }

    private sealed class StubWiFiScanner : IWiFiScanner
    {
        private readonly WlanInterfaceDiscoveryResult _discovery;
        private readonly WiFiScanBatch _batch;

        public StubWiFiScanner(IReadOnlyList<ScannedWiFiNetwork> networks)
            : this(["wlan0"], networks)
        {
        }

        public StubWiFiScanner(IReadOnlyList<string> interfaces, IReadOnlyList<ScannedWiFiNetwork> networks)
        {
            _discovery = new WlanInterfaceDiscoveryResult(
                interfaces.Count > 0,
                interfaces.Count > 0 ? null : "No WLAN interfaces available.",
                interfaces.Select(static i => new WlanInterfaceInfo(i, "Up", true)).ToArray());
            _batch = new WiFiScanBatch(interfaces, networks);
        }

        public Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default) =>
            Task.FromResult(_discovery);

        public Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default) =>
            Task.FromResult(_batch);
    }
}
