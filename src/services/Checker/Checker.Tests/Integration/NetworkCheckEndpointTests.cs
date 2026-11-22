using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Checker.Tests.Integration;

public sealed class NetworkCheckEndpointTests
{
    [Fact]
    public async Task Check_ValidFormatsAndSecurityHeaders_ShouldNormalizeAndProtectHeaders()
    {
        using var factory = new CheckerApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());

        var response = await client.GetAsync("/networks/check?bssid=60-60-1f-aa-bb-cc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("DENY", string.Join(",", response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", string.Join(",", response.Headers.GetValues("Referrer-Policy")));

        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);
        Assert.True(doc!.RootElement.GetProperty("isDroneStation").GetBoolean());
        Assert.Equal("Confirmed", doc.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("60:60:1F:AA:BB:CC", doc.RootElement.GetProperty("normalizedBssid").GetString());
        Assert.Equal("DJI", doc.RootElement.GetProperty("brandMark").GetString());
    }

    [Theory]
    [InlineData("/networks/check")]
    [InlineData("/networks/check?bssid=invalid-mac")]
    [InlineData("/networks/check?bssid=00:00:00:00:00:00")]
    [InlineData("/networks/check?bssid=FF:FF:FF:FF:FF:FF")]
    [InlineData("/networks/check?bssid=60:60:1F:12:34:56&ssid=")]
    [InlineData("/networks/check?bssid=60:60:1F:12:34:56&ssid=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("/networks/check?bssid=60:60:1F:12:34:56&extra=unexpected")]
    public async Task Check_InvalidOrUnexpectedInputs_ShouldReturn400ValidationProblem(string url)
    {
        using var factory = new CheckerApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownRouteOrMethod_ShouldReturnProblemDetails()
    {
        using var factory = new CheckerApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());

        var notFound = await client.GetAsync("/networks/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("application/problem+json", notFound.Content.Headers.ContentType?.MediaType);

        var methodNotAllowed = await client.PostAsync("/networks/check?bssid=60:60:1F:12:34:56", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, methodNotAllowed.StatusCode);
        Assert.Equal("application/problem+json", methodNotAllowed.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BurstRequestsExceedingLimit_ShouldReturn429ProblemDetails()
    {
        using var factory = new CheckerApiFactory(rateLimitPerMinute: 3);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var throttled = await client.GetAsync("/networks/check?bssid=60:60:1F:12:34:56");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal("application/problem+json", throttled.Content.Headers.ContentType?.MediaType);
    }
}
