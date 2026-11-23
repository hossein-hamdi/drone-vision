using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Checker.Tests.Integration;

public sealed class NetworkScanEndpointTests
{
    [Fact]
    public async Task Scan_WithoutAuth_ShouldReturn401()
    {
        using var factory = new CheckerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/networks/scan");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scan_WithBearerToken_ShouldReturnOnlyDronesAndSupportIncludePossible()
    {
        var stubScanner = new StubWiFiScanner(
            ["wlan0", "wlan1"],
            [
                new ScannedWiFiNetwork("00:2A:6A:11:22:33", "OfficeRouter", 95, -52, "2.4 GHz", 2412, 1, "wlan0"),
                new ScannedWiFiNetwork("00:2A:6A:11:22:33", "OfficeRouter", 82, -59, "2.4 GHz", 2412, 1, "wlan1"),
                new ScannedWiFiNetwork("60:60:1F:12:34:56", "Mavic-3-Pro", 75, -62, "5.8 GHz", 5745, 149, "wlan0"),
                new ScannedWiFiNetwork("60-60-1F-12-34-56", "Mavic-3-Pro", 88, -56, "5.8 GHz", 5745, 149, "wlan1"),
                new ScannedWiFiNetwork("20:F4:1B:44:55:66", "SYMA_FPV_1234", 72, -64, "2.4 GHz", 2437, 6, "wlan0"),
                new ScannedWiFiNetwork("24:0A:C4:11:22:33", "ESP-IoT-Sensor", 50, -75, "2.4 GHz", 2462, 11, "wlan1")
            ]);

        using var baseFactory = new CheckerApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWiFiScanner>();
            services.AddScoped<IWiFiScanner>(_ => stubScanner);
        }));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", baseFactory.CreateToken());

        var response = await client.GetAsync("/networks/scan");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = doc!.RootElement;
        Assert.Equal(4, root.GetProperty("totalNetworksScanned").GetInt32());
        Assert.Equal(2, root.GetProperty("droneCount").GetInt32());
        Assert.Equal(
            ["wlan0", "wlan1"],
            root.GetProperty("scannedInterfaces").EnumerateArray().Select(static x => x.GetString()!).ToArray());

        var first = root.GetProperty("drones")[0];
        Assert.Equal("60:60:1F:12:34:56", first.GetProperty("bssid").GetString());
        Assert.Equal("Mavic-3-Pro", first.GetProperty("ssid").GetString());
        Assert.True(first.GetProperty("isDroneStation").GetBoolean());
        Assert.Equal("Confirmed", first.GetProperty("verdict").GetString());
        Assert.True(first.GetProperty("confidenceScore").GetInt32() >= 90);
        Assert.Equal("DJI", first.GetProperty("brandMark").GetString());
        Assert.NotEmpty(first.GetProperty("candidateModels").EnumerateArray());
        Assert.NotEmpty(first.GetProperty("frequencyBands").EnumerateArray());
        Assert.True(first.TryGetProperty("stationRole", out _));
        Assert.Equal(88, first.GetProperty("signalStrengthPercent").GetInt32());
        Assert.Equal(-56, first.GetProperty("rssiDbm").GetInt32());
        Assert.Equal("5.8 GHz", first.GetProperty("band").GetString());
        Assert.Equal(5745, first.GetProperty("frequencyMhz").GetInt32());
        Assert.Equal(149, first.GetProperty("channel").GetInt32());
        Assert.Equal(
            ["wlan0", "wlan1"],
            first.GetProperty("interfaceNames").EnumerateArray().Select(static x => x.GetString()!).ToArray());

        string[] removedScanFields =
        [
            "radioType", "authentication", "matchSource", "hardwareVendor",
            "chipsetExclusivity", "frequencyRangesMhz", "radioProtocols",
            "signalRangeTier", "matchedPrefix", "matchedSsidPattern",
            "isLocallyAdministered", "notes", "supportedFrequencyBands"
        ];
        foreach (var field in removedScanFields)
        {
            Assert.False(first.TryGetProperty(field, out _), $"Field '{field}' should be omitted from streamlined /networks/scan drone item.");
        }

        var withPossible = await client.GetAsync("/networks/scan?includePossible=true");
        Assert.Equal(HttpStatusCode.OK, withPossible.StatusCode);
        using var possibleDoc = await withPossible.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(3, possibleDoc!.RootElement.GetProperty("droneCount").GetInt32());
    }

    [Fact]
    public async Task Scan_WhenHardwareUnavailable_ShouldReturn503ProblemDetails()
    {
        using var baseFactory = new CheckerApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWiFiScanner>();
            services.AddScoped<IWiFiScanner>(_ => new UnavailableScanner());
        }));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", baseFactory.CreateToken());

        var response = await client.GetAsync("/networks/scan");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // Non-scan endpoints continue operating normally when wireless hardware is unavailable (Windows Docker simulation).
        var checkResponse = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");
        Assert.Equal(HttpStatusCode.OK, checkResponse.StatusCode);

        var healthResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    [Fact]
    public async Task Scan_WithUnexpectedQueryParameter_ShouldReturn400()
    {
        using var baseFactory = new CheckerApiFactory();
        using var client = baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", baseFactory.CreateToken());

        var response = await client.GetAsync("/networks/scan?bogus=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class StubWiFiScanner(
        IReadOnlyList<string> interfaces,
        IReadOnlyList<ScannedWiFiNetwork> networks) : IWiFiScanner
    {
        public Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default) =>
            Task.FromResult(new WlanInterfaceDiscoveryResult(
                interfaces.Count > 0,
                interfaces.Count > 0 ? null : "No WLAN interfaces available.",
                interfaces.Select(static i => new WlanInterfaceInfo(i, "Up", true)).ToArray()));

        public Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default) =>
            Task.FromResult(new WiFiScanBatch(interfaces, networks));
    }

    private sealed class UnavailableScanner : IWiFiScanner
    {
        public Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default) =>
            Task.FromResult(new WlanInterfaceDiscoveryResult(false, "No wireless interface available.", []));

        public Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default) =>
            throw ApiException.ServiceUnavailable("No wireless interface available.");
    }
}
