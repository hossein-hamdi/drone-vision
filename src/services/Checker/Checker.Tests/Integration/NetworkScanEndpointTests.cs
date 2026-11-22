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
        [
            new ScannedWiFiNetwork("00:2A:6A:11:22:33", "OfficeRouter", 95, -52, "2.4 GHz", 2412, 1, "802.11ac", "WPA2-Personal"),
            new ScannedWiFiNetwork("60:60:1F:12:34:56", "Mavic-3-Pro", 88, -56, "5.8 GHz", 5745, 149, "802.11ac", "WPA2-Personal"),
            new ScannedWiFiNetwork("20:F4:1B:44:55:66", "SYMA_FPV_1234", 72, -64, "2.4 GHz", 2437, 6, "802.11n", "Open"),
            new ScannedWiFiNetwork("24:0A:C4:11:22:33", "ESP-IoT-Sensor", 50, -75, "2.4 GHz", 2462, 11, "802.11n", "WPA2-Personal")
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

        var first = root.GetProperty("drones")[0];
        Assert.Equal("Mavic-3-Pro", first.GetProperty("ssid").GetString());
        Assert.Equal(88, first.GetProperty("signalStrengthPercent").GetInt32());
        Assert.Equal("5.8 GHz", first.GetProperty("band").GetString());
        Assert.Equal(5745, first.GetProperty("frequencyMhz").GetInt32());
        Assert.Equal("DJI", first.GetProperty("brandMark").GetString());

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

    private sealed class StubWiFiScanner(IReadOnlyList<ScannedWiFiNetwork> networks) : IWiFiScanner
    {
        public Task<IReadOnlyList<ScannedWiFiNetwork>> ScanAsync(CancellationToken ct = default) =>
            Task.FromResult(networks);
    }

    private sealed class UnavailableScanner : IWiFiScanner
    {
        public Task<IReadOnlyList<ScannedWiFiNetwork>> ScanAsync(CancellationToken ct = default) =>
            throw ApiException.ServiceUnavailable("No wireless interface available.");
    }
}
