using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Checker.Application.Abstractions;
using Checker.Application.Common;
using Checker.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Checker.Tests.Integration;

public sealed class WlanInterfacesEndpointTests
{
    [Fact]
    public async Task Interfaces_WithoutAuth_ShouldReturn401()
    {
        using var factory = new CheckerApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/networks/interfaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Interfaces_WithUnexpectedQueryParameter_ShouldReturn400ValidationProblem()
    {
        using var factory = new CheckerApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());

        var response = await client.GetAsync("/networks/interfaces?bogus=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Interfaces_WhenWlanAdaptersExist_ShouldReturnAvailableTrueAndAdaptersWithSecurityHeaders()
    {
        var stubScanner = new StubInterfaceScanner(new WlanInterfaceDiscoveryResult(
            Available: true,
            Reason: null,
            Interfaces:
            [
                new WlanInterfaceInfo("wlan0", "Up", true, "802.11 (managed)", "00:11:22:33:44:55"),
                new WlanInterfaceInfo("wlan1", "Disconnected", true, "802.11 (managed)", "66:77:88:99:AA:BB")
            ]));

        using var baseFactory = new CheckerApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWiFiScanner>();
            services.AddScoped<IWiFiScanner>(_ => stubScanner);
        }));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", baseFactory.CreateToken());

        var response = await client.GetAsync("/networks/interfaces");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("DENY", string.Join(",", response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", string.Join(",", response.Headers.GetValues("Referrer-Policy")));

        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = doc!.RootElement;
        Assert.True(root.GetProperty("available").GetBoolean());
        Assert.False(root.TryGetProperty("reason", out _));

        var interfaces = root.GetProperty("interfaces").EnumerateArray().ToArray();
        Assert.Equal(2, interfaces.Length);
        Assert.Equal("wlan0", interfaces[0].GetProperty("name").GetString());
        Assert.Equal("Up", interfaces[0].GetProperty("state").GetString());
        Assert.True(interfaces[0].GetProperty("isUsableForScan").GetBoolean());
        Assert.Equal("00:11:22:33:44:55", interfaces[0].GetProperty("macAddress").GetString());
    }

    [Fact]
    public async Task Interfaces_WhenUnavailableInWindowsDocker_ShouldReturn200WithAvailableFalseAndEmptyList()
    {
        const string reason = "Live wireless scanning is unavailable because no accessible WLAN interfaces were detected in this environment.";
        var unavailableScanner = new StubInterfaceScanner(new WlanInterfaceDiscoveryResult(false, reason, []));

        using var baseFactory = new CheckerApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWiFiScanner>();
            services.AddScoped<IWiFiScanner>(_ => unavailableScanner);
        }));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", baseFactory.CreateToken());

        var response = await client.GetAsync("/networks/interfaces");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = doc!.RootElement;
        Assert.False(root.GetProperty("available").GetBoolean());
        Assert.Equal(reason, root.GetProperty("reason").GetString());
        Assert.Empty(root.GetProperty("interfaces").EnumerateArray());
    }

    [Fact]
    public async Task Interfaces_BurstExceedingLimit_ShouldReturn429ProblemDetails()
    {
        var unavailableScanner = new StubInterfaceScanner(new WlanInterfaceDiscoveryResult(false, "Unavailable", []));
        using var baseFactory = new CheckerApiFactory(rateLimitPerMinute: 2);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWiFiScanner>();
            services.AddScoped<IWiFiScanner>(_ => unavailableScanner);
        }));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", baseFactory.CreateToken());

        for (var i = 0; i < 2; i++)
        {
            var ok = await client.GetAsync("/networks/interfaces");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var throttled = await client.GetAsync("/networks/interfaces");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal("application/problem+json", throttled.Content.Headers.ContentType?.MediaType);
    }

    private sealed class StubInterfaceScanner(WlanInterfaceDiscoveryResult discovery) : IWiFiScanner
    {
        public Task<WlanInterfaceDiscoveryResult> DiscoverInterfacesAsync(CancellationToken ct = default) =>
            Task.FromResult(discovery);

        public Task<WiFiScanBatch> ScanAsync(CancellationToken ct = default) =>
            throw ApiException.ServiceUnavailable(discovery.Reason ?? "Unavailable");
    }
}
