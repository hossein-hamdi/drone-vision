using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Checker.API.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Checker.Tests.Integration;

public sealed class BrowserSessionAuthTests
{
    private static readonly string ValidFormatCookie = new('a', 86);

    [Fact]
    public async Task Check_WithValidBrowserRefreshCookie_ShouldAuthenticateAndReturn200()
    {
        using var factory = new CheckerApiFactory()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IManagementSessionValidator>();
                services.AddSingleton<IManagementSessionValidator>(
                    new StubManagementSessionValidator(expectedToken: ValidFormatCookie, userId: 7, userName: "admin"));
            }));

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/networks/check?bssid=60:60:1F:12:34:56");
        request.Headers.Add("Cookie", $"{BrowserSessionHandler.CookieName}={ValidFormatCookie}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(doc!.RootElement.GetProperty("isDroneStation").GetBoolean());
        Assert.Equal("DJI", doc.RootElement.GetProperty("brandMark").GetString());
    }

    [Fact]
    public async Task Check_WithRevokedBrowserRefreshCookie_ShouldReturn401AndDeleteCookie()
    {
        using var factory = new CheckerApiFactory()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IManagementSessionValidator>();
                services.AddSingleton<IManagementSessionValidator>(
                    new StubManagementSessionValidator(expectedToken: "other-token", userId: 7, userName: "admin"));
            }));

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/networks/check?bssid=60:60:1F:12:34:56");
        request.Headers.Add("Cookie", $"{BrowserSessionHandler.CookieName}={ValidFormatCookie}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            v => v.Contains($"{BrowserSessionHandler.CookieName}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_WithMalformedBrowserCookie_ShouldReturn401WithoutCallingManagement()
    {
        var tracker = new TrackingManagementSessionValidator();
        using var factory = new CheckerApiFactory()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IManagementSessionValidator>();
                services.AddSingleton<IManagementSessionValidator>(tracker);
            }));

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/networks/check?bssid=60:60:1F:12:34:56");
        request.Headers.Add("Cookie", $"{BrowserSessionHandler.CookieName}=too-short");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, tracker.CallCount);
    }

    private sealed class StubManagementSessionValidator(string expectedToken, int userId, string userName)
        : IManagementSessionValidator
    {
        public Task<ValidatedManagementSession?> ValidateRefreshCookieAsync(string refreshCookieValue, CancellationToken ct) =>
            Task.FromResult(refreshCookieValue == expectedToken
                ? new ValidatedManagementSession(userId, userName)
                : null);
    }

    private sealed class TrackingManagementSessionValidator : IManagementSessionValidator
    {
        public int CallCount { get; private set; }

        public Task<ValidatedManagementSession?> ValidateRefreshCookieAsync(string refreshCookieValue, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult<ValidatedManagementSession?>(null);
        }
    }
}
